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

        // Initialize the DBAccess class with ConfigurationManager and setup the connection string
        static DBAccess()
        {
            // Use ConfigurationManager to set the SQLite path and connection string
            if (ConfigurationManager.CurrentSettings == null)
            {
                ConfigurationManager.LoadOrCreateAppSettings();
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
        public static async Task<bool> CheckDatabaseIntegrityAsync()
        {
            try
            {
                await OpenConnectionAsync();

                if (connection != null && connection.State == System.Data.ConnectionState.Open)
                {
                    // Define a list of all required tables and views
                    var requiredDbObjects = new List<string>
                    {
                        "cards",
                        "myCollection",
                        "uniqueManaCostImages",
                        "uniqueManaSymbols",
                        "keyruneImages",
                        "view_allCards",
                        "view_myCollection",
                        "view_cardToken"
                    };

                    // First, check if the database has the expected tables and views
                    if (!await DatabaseHasTablesAndViewsAsync(requiredDbObjects))
                    {
                        Debug.WriteLine("Database does not contain all expected tables and views.");
                        return false; // Consider the absence of expected tables or views as a failure
                    }

                    // Proceed with the integrity check if the tables and views exist
                    using (var command = new SQLiteCommand("PRAGMA quick_check", connection))
                    {
                        var result = await command.ExecuteScalarAsync();
                        string resultString = result?.ToString() ?? "unknown";

                        if (resultString == "ok")
                        {
                            Debug.WriteLine("Database integrity check passed.");
                            return true;
                        }
                        else
                        {
                            Debug.WriteLine($"Database integrity check failed: {resultString}");
                            return false;
                        }
                    }
                }
                else
                {
                    Debug.WriteLine("Failed to open database connection for integrity check.");
                    return false;
                }
            }
            catch (SQLiteException ex)
            {
                Debug.WriteLine($"SQLite error during database integrity check: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unexpected error during database integrity check: {ex.Message}");
                return false;
            }
            finally
            {
                CloseConnection();
            }
        }
        private static async Task<bool> DatabaseHasTablesAndViewsAsync(List<string> expectedObjects)
        {
            if (connection == null || connection.State != System.Data.ConnectionState.Open)
            {
                return false;
            }

            foreach (var name in expectedObjects)
            {
                string checkExistenceQuery = $"SELECT name FROM sqlite_master WHERE (type='table' OR type='view') AND name='{name}';";
                using (var command = new SQLiteCommand(checkExistenceQuery, connection))
                {
                    var result = await command.ExecuteScalarAsync();
                    if (result == null)
                    {
                        Debug.WriteLine($"Missing expected database object: {name}");
                        return false; // Return false immediately if one of the objects is missing
                    }
                }
            }
            return true; // If all checks pass
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
        public static async Task<List<string>> GetUniqueValuesAsync(string tableName, string columnName)
        {
            List<string> uniqueValues = [];

            try
            {
                string query = $"SELECT DISTINCT {columnName} FROM {tableName};";

                using var command = new SQLiteCommand(query, DBAccess.connection);
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string value = reader[columnName]?.ToString() ?? string.Empty;
                    if (!string.IsNullOrEmpty(value))
                    {
                        uniqueValues.Add(value);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"An error occurred while fetching unique values: {ex.Message}");
                MessageBox.Show($"An error occurred while fetching unique values: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return uniqueValues;
        }
    }
}
