using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;

namespace CollectaMundo.ApplicationServices.Startup
{
    public class AppSettings : IAppSettings
    {
        // Private properties
        private static readonly string LocalAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        private static readonly string AppRootPath = Path.Combine(LocalAppDataPath, "CollectaMundo");
        private static readonly string SqlitePath = Path.Combine(AppRootPath, "CardDatabase");
        private static readonly string UserProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        private readonly string _settingsFilePath;
        private readonly AppSettingsDto _currentSettings;

        // Public properties
        public DatabaseSettings DatabaseSettings { get; private set; } = new();
        public ConnectionStrings ConnectionStrings { get; private set; } = new();
        public PriceInfo PriceInfo { get; private set; } = new();
        public string CardDatabaseUrl => "https://mtgjson.com/api/v5/AllPrintings.sqlite.gz";
        public string CardPricesUrl => "https://mtgjson.com/api/v5/AllPricesToday.json.gz";
        public string UserDownloadsPath => Path.Combine(UserProfilePath, "Downloads");
        public string BackupFolderPath => _currentSettings.BackupFolderPath;
        public string CardImageCachePath => Path.Combine(AppRootPath, "CardImageCache");

        // Constructor
        public AppSettings(string? filePath = null)
        {
            // The optional path allows tests or other callers to use a separate settings file.
            _settingsFilePath = filePath ?? Path.Combine(AppRootPath, "appsettings.json");
            _currentSettings = LoadOrCreateAppSettings();

            DatabaseSettings = _currentSettings.DatabaseSettings;
            ConnectionStrings = _currentSettings.ConnectionStrings;
            PriceInfo = _currentSettings.PriceInfo;
        }
        private AppSettingsDto LoadOrCreateAppSettings()
        {
            if (!File.Exists(_settingsFilePath))
            {
                CreateDefaultAppSettings(_settingsFilePath);
            }

            var json = File.ReadAllText(_settingsFilePath);
            var settings = JsonConvert.DeserializeObject<AppSettingsDto>(json) ?? new AppSettingsDto();

            // Older settings files may not contain a rate. Invalid saved rates also fall back to the default.
            if (settings.PriceInfo.UsdToEurRate <= 0m)
            {
                settings.PriceInfo.UsdToEurRate = PriceInfo.DefaultUsdToEurRate;
            }

            // The connection string is derived from the configured database path.
            settings.ConnectionStrings.SQLiteConnection = BuildSQLiteConnectionString(settings.DatabaseSettings.SQLitePath);

            return settings;
        }
        private static void CreateDefaultAppSettings(string settingsFilePath)
        {
            if (string.IsNullOrWhiteSpace(settingsFilePath))
            {
                throw new ArgumentException("Settings file path is required.", nameof(settingsFilePath));
            }

            var settingsDirectory = Path.GetDirectoryName(Path.GetFullPath(settingsFilePath)) ?? throw new InvalidOperationException("Could not determine the settings directory.");

            Directory.CreateDirectory(settingsDirectory);
            Directory.CreateDirectory(SqlitePath);

            var sqlitePathWithSeparator = Path.EndsInDirectorySeparator(SqlitePath)
                ? SqlitePath
                : SqlitePath + Path.DirectorySeparatorChar;

            var defaultSettings = new AppSettingsDto
            {
                DatabaseSettings = new DatabaseSettings
                {
                    SQLitePath = sqlitePathWithSeparator
                },
                ConnectionStrings = new ConnectionStrings
                {
                    SQLiteConnection = BuildSQLiteConnectionString(SqlitePath)
                },
                PriceInfo = new PriceInfo(),
                BackupFolderPath = Path.Combine(UserProfilePath, "CollectaMundoBackup")
            };

            File.WriteAllText(settingsFilePath, JsonConvert.SerializeObject(defaultSettings, Formatting.Indented));
        }
        private static string BuildSQLiteConnectionString(string databaseDirectory)
        {
            var databasePath = Path.Combine(databaseDirectory, "AllPrintings.sqlite");
            return $"Data Source={databasePath};Version=3;";
        }

        // Settings persistence
        public void PersistPriceInfo(string? updatedDate, string? retailer)
        {
            // Null arguments leave the corresponding saved values unchanged.
            if (updatedDate is not null)
            {
                _currentSettings.PriceInfo.PricesUpdatedDate = updatedDate;
            }

            if (retailer is not null)
            {
                _currentSettings.PriceInfo.Retailer = retailer;
            }

            try
            {
                SaveSettings();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving appsettings.json: {ex.Message}");
            }
        }
        public void PersistExchangeRate(decimal usdToEurRate, DateTimeOffset retrievedAtUtc)
        {
            if (usdToEurRate <= 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(usdToEurRate), "The USD-to-EUR rate must be greater than zero.");
            }

            // Store the rate as EUR per USD and normalize its retrieval time to UTC.
            _currentSettings.PriceInfo.UsdToEurRate = usdToEurRate;
            _currentSettings.PriceInfo.ExchangeRateUpdatedAtUtc = retrievedAtUtc.ToUniversalTime();

            try
            {
                SaveSettings();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving exchange rate to appsettings.json: {ex.Message}");
            }
        }
        public void PersistBackupFolderPath(string newBackupFolderPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(newBackupFolderPath))
                {
                    throw new ArgumentException("Backup folder path cannot be empty.", nameof(newBackupFolderPath));
                }

                if (!Directory.Exists(newBackupFolderPath))
                {
                    Directory.CreateDirectory(newBackupFolderPath);
                }

                _currentSettings.BackupFolderPath = newBackupFolderPath;
                SaveSettings();

                Debug.WriteLine($"Backup folder path successfully updated to: {newBackupFolderPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving appsettings.json: {ex.Message}");
                throw;
            }
        }
        private void SaveSettings()
        {
            // Serialize the complete settings object so updates preserve unrelated settings.
            var json = JsonConvert.SerializeObject(_currentSettings, Formatting.Indented);
            File.WriteAllText(_settingsFilePath, json);
        }
        private class AppSettingsDto
        {
            public DatabaseSettings DatabaseSettings { get; set; } = new();
            public ConnectionStrings ConnectionStrings { get; set; } = new();
            public PriceInfo PriceInfo { get; set; } = new();

            public string BackupFolderPath { get; set; } = Path.Combine(UserProfilePath, "CollectaMundoBackup");
        }
    }
    public class DatabaseSettings
    {
        public string SQLitePath { get; set; } = string.Empty;
    }
    public class ConnectionStrings
    {
        public string SQLiteConnection { get; set; } = string.Empty;
    }
    public class PriceInfo
    {
        public const decimal DefaultUsdToEurRate = 0.8909m;

        public string PricesUpdatedDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd");
        public string Retailer { get; set; } = "cardmarket";
        public decimal UsdToEurRate { get; set; } = DefaultUsdToEurRate;
        public DateTimeOffset? ExchangeRateUpdatedAtUtc { get; set; }
    }
}
