using System.Data.SQLite;
using System.Diagnostics;
using System.Windows;

namespace CollectaMundo
{
    public class DBAccess
    {
        private static string _sqlitePath = string.Empty;
        public static SQLiteConnection? connection; // Instantiate SQLite connection for db access
        public static SQLiteConnection? tempDbConnection; // Instantiate SQLite connection for temporary db access when updating

        // Get the path to the db from the ConfigurationManager
        public static string SqlitePath
        {
            get
            {
                if (_sqlitePath == string.Empty) // Check for default value instead of null
                {
                    _sqlitePath = ConfigurationManager.CurrentSettings.DatabaseSettings.SQLitePath;
                }
                return _sqlitePath;
            }
        }
        public static async Task OpenConnectionAsync()
        {
            try
            {
                if (connection == null)
                {
                    string? connectionString = ConfigurationManager.CurrentSettings.ConnectionStrings.SQLiteConnection;
                    if (string.IsNullOrEmpty(connectionString))
                    {
                        throw new InvalidOperationException("Connection string not found in appsettings.json.");
                    }

                    if (string.IsNullOrEmpty(SqlitePath))
                    {
                        throw new InvalidOperationException("SQLite database path not found in appsettings.json.");
                    }

                    string fullConnectionString = connectionString.Replace("{SQLitePath}", SqlitePath);
                    connection = new SQLiteConnection(fullConnectionString);
                }

                if (connection.State != System.Data.ConnectionState.Open)
                {
                    await connection.OpenAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Opening connection failed: {ex.Message}");
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
                Debug.WriteLine($"Closing connection failed: {ex.Message}");
                MessageBox.Show($"Closing connection failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        public static async Task OptimizeDb()
        {
            List<string> optimizeCommands = new()
            {
                "VACUUM;",
                "ANALYZE;",
                "PRAGMA optimize;"
            };

            // Execute each command asynchronously
            foreach (var item in optimizeCommands)
            {
                using var command = new SQLiteCommand(item, DBAccess.connection);
                await command.ExecuteNonQueryAsync();
            }
        }
    }
}
