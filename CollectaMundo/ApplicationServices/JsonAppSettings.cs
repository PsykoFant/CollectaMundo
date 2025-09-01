using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;

namespace CollectaMundo.ApplicationServices
{
    public class JsonAppSettings : IAppSettings
    {
        // backing POCO
        private static AppSettingsDto CurrentSettings { get; set; } = new();

        // non-nullable, with defaults
        public DatabaseSettings DatabaseSettings { get; private set; } = new();
        public ConnectionStrings ConnectionStrings { get; private set; } = new();
        public PriceInfo PriceInfo { get; private set; } = new();
        public string CardDatabaseUrl => "https://mtgjson.com/api/v5/AllPrintings.sqlite";
        public string CardPricesUrl => "https://mtgjson.com/api/v5/AllPricesToday.json";
        public string UserDownloadsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        private static readonly string appSettingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

        public JsonAppSettings()
        {
            LoadOrCreateAppSettings();
            DatabaseSettings = CurrentSettings.DatabaseSettings;
            ConnectionStrings = CurrentSettings.ConnectionStrings;
            PriceInfo = CurrentSettings.PriceInfo;
        }
        private static void LoadOrCreateAppSettings()
        {
            if (!File.Exists(appSettingsFile))
            {
                CreateDefaultAppSettings();
            }

            // Load the configuration file into strongly typed AppSettingsDto
            var json = File.ReadAllText(appSettingsFile);
            CurrentSettings = JsonConvert.DeserializeObject<AppSettingsDto>(json) ?? new AppSettingsDto();

            // Rebuild the connection string with the loaded SQLitePath
            CurrentSettings.ConnectionStrings.SQLiteConnection = $"Data Source={CurrentSettings.DatabaseSettings.SQLitePath}AllPrintings.sqlite;Version=3;";
        }
        private static void CreateDefaultAppSettings()
        {
            try
            {
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string sqlitePath = Path.Combine(appDataPath, "CollectaMundo", "CardDatabase");

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

                File.WriteAllText(appSettingsFile, JsonConvert.SerializeObject(defaultSettings, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating appsettings.json: {ex.Message}");
                MessageBox.Show($"Error creating appsettings.json: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        public void UpdatePriceInfo(string? updatedDate, string? retailer)
        {
            // Update the PriceInfo fields
            if (updatedDate != null)
            {
                CurrentSettings.PriceInfo.PricesUpdatedDate = updatedDate;
            }
            if (retailer != null)
            {
                CurrentSettings.PriceInfo.Retailer = retailer;
            }

            // Save the updated settings to appsettings.json
            SaveSettings();
        }
        private static void SaveSettings()
        {
            try
            {
                // Serialize the CurrentSettings object back to the JSON file
                string json = JsonConvert.SerializeObject(CurrentSettings, Formatting.Indented);
                File.WriteAllText(appSettingsFile, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving appsettings.json: {ex.Message}");
                MessageBox.Show($"Error saving appsettings.json: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        public static object? GetSetting(string settingPath)
        {
            try
            {
                // Refresh the CurrentSettings object
                LoadOrCreateAppSettings();

                string[] pathParts = settingPath.Split(':');
                object? current = CurrentSettings;

                foreach (var part in pathParts)
                {
                    if (current == null)
                    {
                        return null;
                    }

                    PropertyInfo? property = current.GetType().GetProperty(part);
                    if (property == null)
                    {
                        return null;
                    }

                    current = property.GetValue(current, null);
                }

                return current;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting setting '{settingPath}': {ex.Message}");
                return null;
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
