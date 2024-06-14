using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace CollectaMundo
{
    public class DBAccess
    {
        private static IConfiguration Configuration { get; set; } // For getting db path from db
        private static string _sqlitePath = string.Empty;

        public static SQLiteConnection? connection; // instantiate SQLite connection to use for db access
        public static SQLiteConnection? tempDbConnection; // instantiate SQLite connection to use for temp db access when updating        
        public static string sqlitePath // Get the path to the db
        {
            get
            {
                if (_sqlitePath == string.Empty) // Check for default value instead of null
                {
                    _sqlitePath = Configuration["DatabaseSettings:SQLitePath"] ?? string.Empty;
                }
                return _sqlitePath;
            }
        }

        static DBAccess() // Build the string to the db from appsettings.json
        {
            // Ensure the appsettings.json file is created before reading it
            EnsureAppSettings();

            // Set up configuration
            IConfigurationBuilder builder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            Configuration = builder.Build();
        }

        public static async Task OpenConnectionAsync()
        {
            try
            {
                if (connection == null)
                {
                    string? connectionString = Configuration.GetConnectionString("SQLiteConnection");
                    if (string.IsNullOrEmpty(connectionString))
                    {
                        throw new InvalidOperationException("Connection string not found in appsettings.json.");
                    }

                    if (string.IsNullOrEmpty(sqlitePath))
                    {
                        throw new InvalidOperationException("SQLite database path not found in appsettings.json.");
                    }

                    string fullConnectionString = connectionString.Replace("{SQLitePath}", sqlitePath);
                    connection = new SQLiteConnection(fullConnectionString);
                }

                if (connection.State != System.Data.ConnectionState.Open)
                {
                    await connection.OpenAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Opening connection failed {ex.Message}");
                MessageBox.Show($"Opening connection failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static void CloseConnection()
        {
            try
            {
                if (connection != null && connection.State == System.Data.ConnectionState.Open)
                {
                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Closing connection failed {ex.Message}");
                MessageBox.Show($"Closing connection failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private static void EnsureAppSettings()
        {
            try
            {
                string appSettingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (!File.Exists(appSettingsFile))
                {
                    // Construct the path dynamically
                    string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    string sqlitePath = Path.Combine(appDataPath, "CollectaMundo", "CardDatabase");

                    // Ensure the directory exists
                    Directory.CreateDirectory(sqlitePath);

                    // Create settings object with placeholder in connection string
                    var settings = new
                    {
                        DatabaseSettings = new
                        {
                            SQLitePath = $"{sqlitePath}\\"
                        },
                        ConnectionStrings = new
                        {
                            SQLiteConnection = "Data Source={SQLitePath}AllPrintings.sqlite;Version=3;"
                        }
                    };

                    // Serialize to JSON
                    string json = JsonConvert.SerializeObject(settings, Formatting.Indented);

                    // Write to file
                    File.WriteAllText(appSettingsFile, json);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating appsettings.json: {ex.Message}");
                MessageBox.Show($"Error creating appsettings.json: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

    }
}
