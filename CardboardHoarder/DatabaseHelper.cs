using CardboardHoarder;
using Microsoft.Extensions.Configuration;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;

public class DatabaseHelper
{
    private static IConfiguration Configuration { get; set; }
    static DatabaseHelper()
    {
        // Set up configuration
        var builder = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        Configuration = builder.Build();
    }
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
            string? sqlitePath = Configuration["DatabaseSettings:SQLitePath"];

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
    private static string GetSQLitePath()
    {
        // Retrieve the SQLite database path from appsettings.json
        return Configuration["DatabaseSettings:SQLitePath"] ?? string.Empty;
    }
    public static void CheckDatabaseExistence()
    {
        Debug.WriteLine("Inside CheckDatabaseExistence()");
        try
        {
            // Retrieve the SQLite database path from appsettings.json
            string sqlitePath = GetSQLitePath();
            string databasePath = Path.Combine(sqlitePath, "AllPrintings.sqlite");

            // Check if the database file exists
            if (!File.Exists(databasePath))
            {
                // Output a message to the console
                Debug.WriteLine($"The database file '{databasePath}' does not exist.");
                DownloadDatabaseIfNotExists();
                GenerateCustomDbData();
            }

        }
        catch (Exception ex)
        {
            // Handle exceptions (e.g., log, show error message, etc.)
            Debug.WriteLine($"Error while checking database existence: {ex.Message}");
        }
    }
    #region Download card database and create tables for custom data
    public static void DownloadDatabaseIfNotExists()
    {

        // Create and show the DownloadProgressWindow
        DownloadWindow downloadWindow = new DownloadWindow();
        downloadWindow.Show();

        try
        {
            // Retrieve the SQLite database path from appsettings.json
            string sqlitePath = Configuration["DatabaseSettings:SQLitePath"] ?? "defaultPath";
            string databasePath = Path.Combine(sqlitePath, "AllPrintings.sqlite");

            // Check if the database file exists
            if (!File.Exists(databasePath))
            {
                // Output a message to the console
                Debug.WriteLine($"The database file '{databasePath}' does not exist. Downloading...");

                // Ensure the directory exists
                Directory.CreateDirectory(sqlitePath);

                // Download the database file from the specified URL using HttpClient
                string downloadUrl = "https://mtgjson.com/api/v5/AllPrintings.sqlite";
                using (HttpClient httpClient = new HttpClient())
                {
                    byte[] fileContent = httpClient.GetByteArrayAsync(downloadUrl).Result;
                    File.WriteAllBytes(databasePath, fileContent);
                }

                Debug.WriteLine($"Download completed. The database file '{databasePath}' is now available.");

                // Setup the downloaded database
                SetupDatabase(databasePath);
            }
            else
            {
                Debug.WriteLine($"The database file '{databasePath}' already exists.");
            }
        }

        catch (Exception ex)
        {
            // Handle exceptions (e.g., log, show error message, etc.)
            Debug.WriteLine($"Error while downloading database file: {ex.Message}");
        }

        finally
        {
            // Close the DownloadProgressWindow after download completion
            downloadWindow.Close();
        }
    }
    private static void SetupDatabase(string databasePath)
    {
        // Open the downloaded database
        using (SQLiteConnection connection = GetConnection())
        {
            connection.Open();

            // Define tables to create
            Dictionary<string, string> tables = new Dictionary<string, string>
            {
                { "uniqueManaSymbols", "CREATE TABLE IF NOT EXISTS uniqueManaSymbols (uniqueManaSymbol TEXT PRIMARY KEY, manaSymbolImage BLOB);" },
                { "uniqueManaCostImages", "CREATE TABLE IF NOT EXISTS uniqueManaCostImages (uniqueManaCost TEXT PRIMARY KEY, manaCostImage BLOB);" },
                { "cardImageStrings", "CREATE TABLE IF NOT EXISTS cardImageStrings (uuid VARCHAR(36) PRIMARY KEY, imageLink TEXT);" },
                { "keyruneImages", "CREATE TABLE IF NOT EXISTS keyruneImages (setCode TEXT PRIMARY KEY, keyRuneImage BLOB);" }
            };

            // Create the tables
            foreach (var item in tables)
            {
                using (SQLiteCommand command = new SQLiteCommand(item.Value, connection))
                {
                    command.ExecuteNonQuery();
                }

                Debug.WriteLine($"Created table for {item.Key}.");
            }

            // Define indices to create
            Dictionary<string, string> indices = new Dictionary<string, string>
            {
                { "uniqueManaSymbols", "CREATE INDEX IF NOT EXISTS uniqueManaSymbols_uniqueManaSymbol ON uniqueManaSymbols(uniqueManaSymbol);" },
                { "uniqueManaCostImages", "CREATE INDEX IF NOT EXISTS uniqueManaCostImages_uniqueManaCost ON uniqueManaCostImages(uniqueManaCost);" },
                { "cardImageStrings", "CREATE INDEX IF NOT EXISTS cardImageStrings_uuid ON cardImageStrings(uuid);" },
                { "keyruneImages", "CREATE INDEX IF NOT EXISTS keyruneImages_setCode ON keyruneImages(setCode);" },
            };

            // Create the indices
            foreach (var item in indices)
            {
                using (SQLiteCommand command = new SQLiteCommand(item.Value, connection))
                {
                    command.ExecuteNonQuery();
                }

                Debug.WriteLine($"Created index for {item.Key}.");
            }

            connection.Close();
        }
    }
    #endregion

    // Husk at lave private
    public static void GenerateCustomDbData()
    {
        GenerateManaSymbolsFromSvg();
    }

    public static void GenerateManaSymbolsFromSvg()
    {
        try
        {
            List<string> uniqueManaCosts = GetUniqueValues("cards", "manaCost");
            List<string> uniqueSymbols = new List<string>();

            foreach (var manaCost in uniqueManaCosts)
            {
                // Use regex to match all occurrences of values between '{' and '}'
                MatchCollection matches = Regex.Matches(manaCost, @"\{(.*?)\}");

                foreach (Match match in matches)
                {
                    string value = match.Groups[1].Value;

                    // Add to the uniqueSymbols list if not already present
                    if (!uniqueSymbols.Contains(value))
                    {
                        uniqueSymbols.Add(value);
                    }
                }
            }

            // Insert unique symbols into the 'uniqueManaSymbols' table if it's not already there
            foreach (var symbol in uniqueSymbols)
            {
                InsertOrUpdateSymbolInTable(symbol, "uniqueManaSymbols", "uniqueManaSymbol");
            }

            Debug.WriteLine("Insertion of uniqueManaSymbols completed.");
        }
        catch (Exception ex)
        {
            // Handle exceptions (e.g., log, show error message, etc.)
            Debug.WriteLine($"Error during insertion of uniqueManaSymbols: {ex.Message}");
        }
    }

    private static void InsertOrUpdateSymbolInTable(string symbol, string tableName, string columnName)
    {
        try
        {
            using (SQLiteConnection connection = GetConnection())
            {
                connection.Open();

                // Check if the symbol already exists in the table
                using (SQLiteCommand selectCommand = new SQLiteCommand(
                    $"SELECT COUNT(*) FROM {tableName} WHERE {columnName} = @symbol",
                    connection))
                {
                    selectCommand.Parameters.AddWithValue("@symbol", symbol);

                    int count = Convert.ToInt32(selectCommand.ExecuteScalar());

                    if (count > 0)
                    {
                        // Symbol exists, perform an update
                        using (SQLiteCommand updateCommand = new SQLiteCommand(
                            $"UPDATE {tableName} SET {columnName} = @symbol WHERE {columnName} = @symbol",
                            connection))
                        {
                            updateCommand.Parameters.AddWithValue("@symbol", symbol);
                            updateCommand.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        // Symbol doesn't exist, perform an insert
                        using (SQLiteCommand insertCommand = new SQLiteCommand(
                            $"INSERT INTO {tableName} ({columnName}) VALUES (@symbol)",
                            connection))
                        {
                            insertCommand.Parameters.AddWithValue("@symbol", symbol);
                            insertCommand.ExecuteNonQuery();
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Handle exceptions (e.g., log, show error message, etc.)
            Debug.WriteLine($"Error during insertion or update: {ex.Message}");
        }
    }







    private static List<string> GetUniqueValues(string tableName, string columnName)
    {
        List<string> uniqueValues = new List<string>();

        using (SQLiteConnection connection = GetConnection())
        {
            connection.Open();

            string query = $"SELECT DISTINCT {columnName} FROM {tableName};";

            using (SQLiteCommand command = new SQLiteCommand(query, connection))
            {
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string value = reader[columnName]?.ToString();
                        if (!string.IsNullOrEmpty(value))
                        {
                            uniqueValues.Add(value);
                        }
                    }
                }
            }

            connection.Close();
        }

        return uniqueValues;
    }
}





