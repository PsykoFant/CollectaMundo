using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;

namespace CollectaMundo.ApplicationServices.Shared
{
    public class AppSettings : IAppSettings
    {
        private readonly AppSettingsDto _currentSettings;

        private readonly string _settingsFilePath;
        private static readonly string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        private static readonly string appRootPath = Path.Combine(localAppDataPath, "CollectaMundo");
        private static readonly string sqlitePath = Path.Combine(appRootPath, "CardDatabase");

        // non-nullable, with defaults
        public DatabaseSettings DatabaseSettings { get; private set; } = new();
        public ConnectionStrings ConnectionStrings { get; private set; } = new();
        public PriceInfo PriceInfo { get; private set; } = new();
        public string CardDatabaseUrl => "https://mtgjson.com/api/v5/AllPrintings.sqlite.gz";
        public string CardPricesUrl => "https://mtgjson.com/api/v5/AllPricesToday.json.gz";

        private static readonly string _userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        public string UserDownloadsPath => Path.Combine(_userProfile, "Downloads");
        public string BackupFolderPath => _currentSettings.BackupFolderPath;

        public string CardImageCachePath => Path.Combine(appRootPath, "CardImageCache");
        public AppSettings(string? filePath = null)
        {
            _settingsFilePath = filePath ?? Path.Combine(appRootPath, "appsettings.json");
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
            Directory.CreateDirectory(sqlitePath);

            var sqlitePathWithSeparator = Path.EndsInDirectorySeparator(sqlitePath)
                ? sqlitePath
                : sqlitePath + Path.DirectorySeparatorChar;

            var defaultSettings = new AppSettingsDto
            {
                DatabaseSettings = new DatabaseSettings
                {
                    SQLitePath = sqlitePathWithSeparator
                },
                ConnectionStrings = new ConnectionStrings
                {
                    SQLiteConnection = BuildSQLiteConnectionString(sqlitePath)
                },
                PriceInfo = new PriceInfo(),
                BackupFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "CollectaMundoBackup")
            };

            File.WriteAllText(
                settingsFilePath,
                JsonConvert.SerializeObject(defaultSettings, Formatting.Indented));
        }
        private static string BuildSQLiteConnectionString(string databaseDirectory)
        {
            var databasePath = Path.Combine(databaseDirectory, "AllPrintings.sqlite");
            return $"Data Source={databasePath};Version=3;";
        }
        public void PersistPriceInfo(string? updatedDate, string? retailer)
        {
            // Update the PriceInfo fields
            if (updatedDate != null)
            {
                _currentSettings.PriceInfo.PricesUpdatedDate = updatedDate;
            }
            if (retailer != null)
            {
                _currentSettings.PriceInfo.Retailer = retailer;
            }

            // Save the updated settings to appsettings.json
            try
            {
                // Serialize the CurrentSettings object back to the JSON file
                string json = JsonConvert.SerializeObject(_currentSettings, Formatting.Indented);
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving appsettings.json: {ex.Message}");
            }
        }
        public void PersistBackupFolderPath(string newBackupFolderPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(newBackupFolderPath))
                    throw new ArgumentException("Backup folder path cannot be empty.", nameof(newBackupFolderPath));

                // Ensure the directory exists
                if (!Directory.Exists(newBackupFolderPath))
                    Directory.CreateDirectory(newBackupFolderPath);

                // Update the current settings object
                _currentSettings.BackupFolderPath = newBackupFolderPath;

                // Serialize and persist to appsettings.json
                string json = JsonConvert.SerializeObject(_currentSettings, Formatting.Indented);
                File.WriteAllText(_settingsFilePath, json);

                Debug.WriteLine($"Backup folder path successfully updated to: {newBackupFolderPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving appsettings.json: {ex.Message}");
                throw; // rethrow to let service layer handle error reporting
            }
        }

    }
    internal class AppSettingsDto
    {
        public DatabaseSettings DatabaseSettings { get; set; } = new DatabaseSettings();
        public ConnectionStrings ConnectionStrings { get; set; } = new ConnectionStrings();
        public PriceInfo PriceInfo { get; set; } = new PriceInfo();

        static readonly string defaultBackupPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "CollectaMundoBackup");
        public string BackupFolderPath { get; set; } = defaultBackupPath;
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
        // Default to today in format YYYY-MM-DD
        public string PricesUpdatedDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd");
        public string Retailer { get; set; } = "cardmarket";
        public decimal? UsdToEurRate { get; set; }
    }
}
