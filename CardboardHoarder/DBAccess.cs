using Microsoft.Extensions.Configuration;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;

namespace CardboardHoarder
{
    public class DBAccess
    {
        private static IConfiguration Configuration { get; set; } // For getting db path from db
        private static string _sqlitePath = string.Empty;

        public static SQLiteConnection? connection; // instantiate SQLite connection to use for db access
        private static SQLiteConnection? temDbConnection; // instantiate SQLite connection to use for temp db access when updating        

        private static string downloadsPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        public static string newDatabasePath = Path.Combine(downloadsPath, "Downloads", "AllPrintings.sqlite");
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
            // Set up configuration
            IConfigurationBuilder builder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            Configuration = builder.Build();
        }
        public static async Task OpenConnectionAsync(bool openRegularDB)
        {
            try
            {
                // Open the regular card database
                if (openRegularDB)
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

                        connection = new SQLiteConnection(connectionString.Replace("{SQLitePath}", sqlitePath));
                    }

                    if (connection.State != System.Data.ConnectionState.Open)
                    {
                        await connection.OpenAsync();
                    }
                }
                // Open the downloaded temp database used when updating db
                else
                {
                    if (temDbConnection == null)
                    {
                        temDbConnection = new SQLiteConnection(newDatabasePath);
                    }

                    if (temDbConnection.State != System.Data.ConnectionState.Open)
                    {
                        await temDbConnection.OpenAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Opening connection failed {ex.Message}");
            }
        }
        public static void CloseConnection(bool closeRegularDB)
        {
            try
            {
                var conn = closeRegularDB ? connection : temDbConnection;
                if (conn != null && conn.State == System.Data.ConnectionState.Open)
                {
                    conn.Close();
                }

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Closing connection failed {ex.Message} (closeRegularDB was set to {closeRegularDB.ToString()})");
            }
        }



        // Den her bliver kun brugt af debug-felter på mainwindow
        public static SQLiteConnection GetConnection()
        {
            try
            {
                // Retrieve the connection string from appsettings.json
                string? connectionString = Configuration.GetConnectionString("SQLiteConnection");

                // Check for null and provide a default value or handle the case accordingly
                if (connectionString == null)
                {
                    throw new InvalidOperationException("Connection string not found in appsettings.json.");
                }

                // Retrieve the SQLite database path from appsettings.json            

                // Check for null and provide a default value or handle the case accordingly
                if (sqlitePath == null)
                {
                    throw new InvalidOperationException("SQLite database path not found in appsettings.json.");
                }

                // Build the connection string using the retrieved path
                string fullConnectionString = connectionString.Replace("{SQLitePath}", sqlitePath);

                // Check if the database file exists before creating the connection
                string databasePath = Path.Combine(sqlitePath, "AllPrintings.sqlite");

                if (!File.Exists(databasePath))
                {
                    throw new InvalidOperationException($"Database file '{databasePath}' does not exist.");
                }

                // Create and return SQLiteConnection
                return new SQLiteConnection(fullConnectionString);
            }
            catch (Exception ex)
            {
                // Handle the exception (e.g., log, show error message, etc.)
                Debug.WriteLine($"Error: {ex.Message}");
                throw;
            }
        }

    }
}
