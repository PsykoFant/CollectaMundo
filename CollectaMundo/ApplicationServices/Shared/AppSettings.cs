using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace CollectaMundo.ApplicationServices.Shared
{
    public class AppSettings : IAppSettings
    {
        private readonly AppSettingsDto _currentSettings;

        private readonly string _settingsFilePath;
        private static readonly string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        private static readonly string sqlitePath = Path.Combine(appDataPath, "CollectaMundo", "CardDatabase");

        // non-nullable, with defaults
        public DatabaseSettings DatabaseSettings { get; private set; } = new();
        public ConnectionStrings ConnectionStrings { get; private set; } = new();
        public PriceInfo PriceInfo { get; private set; } = new();
        public string CardDatabaseUrl => "https://mtgjson.com/api/v5/AllPrintings.sqlite";
        public string CardPricesUrl => "https://mtgjson.com/api/v5/AllPricesToday.json";

        private static readonly string _userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        public string UserDownloadsPath => Path.Combine(_userProfile, "Downloads");
        public string BackupFolderPath => Path.Combine(_userProfile, "CollectaMundoBackup");
        public string CardImageCachePath => Path.Combine(appDataPath, "CollectaMundo", "CardImageCache");
        public AppSettings(string? filePath = null)
        {
            _settingsFilePath = filePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
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
            settings.ConnectionStrings.SQLiteConnection = $"Data Source={settings.DatabaseSettings.SQLitePath}AllPrintings.sqlite;Version=3;";
            return settings;
        }
        private static void CreateDefaultAppSettings(string settingsFilePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(settingsFilePath))
                    throw new ArgumentException("Settings file path is required.", nameof(settingsFilePath));

                Directory.CreateDirectory(sqlitePath);

                var defaultSettings = new AppSettingsDto
                {
                    DatabaseSettings = new DatabaseSettings { SQLitePath = $"{sqlitePath}\\" },
                    ConnectionStrings = new ConnectionStrings
                    {
                        SQLiteConnection = $"Data Source={sqlitePath}\\AllPrintings.sqlite;Version=3;"
                    },
                    PriceInfo = new PriceInfo() // Defaults
                };

                File.WriteAllText(settingsFilePath, JsonConvert.SerializeObject(defaultSettings, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating appsettings.json: {ex.Message}");
                MessageBox.Show($"Error creating appsettings.json: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
                MessageBox.Show($"Error saving appsettings.json: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    internal class AppSettingsDto
    {
        public DatabaseSettings DatabaseSettings { get; set; } = new DatabaseSettings();
        public ConnectionStrings ConnectionStrings { get; set; } = new ConnectionStrings();
        public PriceInfo PriceInfo { get; set; } = new PriceInfo();
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
        public string PricesUpdatedDate { get; set; } = string.Empty;
        public string Retailer { get; set; } = "cardmarket";
    }

}
