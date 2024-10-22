using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace CollectaMundo
{
    public static class ConfigurationManager
    {
        private static readonly string appSettingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        public static AppSettings CurrentSettings { get; private set; }
        static ConfigurationManager()
        {
            // Initialize the CurrentSettings by loading the configuration file or creating a new one
            LoadOrCreateAppSettings();
            CurrentSettings ??= new AppSettings(); // Ensure it is not null
        }
        public static void LoadOrCreateAppSettings()
        {
            if (!File.Exists(appSettingsFile))
            {
                CreateDefaultAppSettings();
            }

            // Load the configuration file into strongly typed AppSettings
            var json = File.ReadAllText(appSettingsFile);
            CurrentSettings = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();

            // Rebuild the connection string with the loaded SQLitePath
            CurrentSettings.ConnectionStrings.SQLiteConnection =
                $"Data Source={CurrentSettings.DatabaseSettings.SQLitePath}AllPrintings.sqlite;Version=3;";
        }
        private static void CreateDefaultAppSettings()
        {
            try
            {
                // Construct the default SQLite path
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string sqlitePath = Path.Combine(appDataPath, "CollectaMundo", "CardDatabase");

                // Ensure the directory exists
                Directory.CreateDirectory(sqlitePath);

                // Create default settings
                var defaultSettings = new AppSettings
                {
                    DatabaseSettings = new DatabaseSettings { SQLitePath = $"{sqlitePath}\\" },
                    ConnectionStrings = new ConnectionStrings
                    {
                        SQLiteConnection = $"Data Source={sqlitePath}\\AllPrintings.sqlite;Version=3;"
                    },
                    PriceInfo = new PriceInfo() // Initialize default values
                };

                // Serialize to JSON and write to file
                File.WriteAllText(appSettingsFile, JsonConvert.SerializeObject(defaultSettings, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating appsettings.json: {ex.Message}");
                MessageBox.Show($"Error creating appsettings.json: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        public static void UpdatePriceInfo(string updatedDate, string defaultPrice = "Avg")
        {
            try
            {
                // Update the PriceInfo fields
                CurrentSettings.PriceInfo.PricesUpdatedDate = updatedDate;
                CurrentSettings.PriceInfo.DefaultPrice = defaultPrice;

                // Save the updated settings to appsettings.json
                SaveSettings();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating PriceInfo in appsettings.json: {ex.Message}");
                MessageBox.Show($"Error updating PriceInfo in appsettings.json: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
    }
    public class AppSettings
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
        public string DefaultPrice { get; set; } = "Avg";
    }

}
