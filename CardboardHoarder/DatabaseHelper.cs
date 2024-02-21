using CardboardHoarder;
using Microsoft.Extensions.Configuration;
using System;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;

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

                // https://mtgjson.com/api/v5/AllPrintings.sqlite
            }

        }
        catch (Exception ex)
        {
            // Handle exceptions (e.g., log, show error message, etc.)
            Debug.WriteLine($"Error while checking database existence: {ex.Message}");
        }
    }
    public static void DownloadDatabaseIfNotExists()
    {
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

                // Create and show the DownloadProgressWindow
                DownloadProgressWindow downloadProgressWindow = new DownloadProgressWindow();
                downloadProgressWindow.Show();

                // Download the database file from the specified URL using HttpClient
                string downloadUrl = "https://mtgjson.com/api/v5/AllPrintings.sqlite";
                using (HttpClient httpClient = new HttpClient())
                {
                    byte[] fileContent = httpClient.GetByteArrayAsync(downloadUrl).Result;
                    File.WriteAllBytes(databasePath, fileContent);
                }

                Debug.WriteLine($"Download completed. The database file '{databasePath}' is now available.");

                // Open the downloaded database
                using (SQLiteConnection connection = GetConnection())
                {
                    connection.Open();

                    // Create 'uniqueManaSymbols' table if it doesn't exist
                    using (SQLiteCommand command = new SQLiteCommand(
                        "CREATE TABLE IF NOT EXISTS uniqueManaSymbols (uniqueManaSymbol TEXT PRIMARY KEY, manaSymbolImage BLOB);",
                        connection))
                    {
                        command.ExecuteNonQuery();
                    }

                    // Create 'uniqueManaSymbols' index
                    using (SQLiteCommand command = new SQLiteCommand(
                        "CREATE INDEX IF NOT EXISTS uniqueManaSymbols_uniqueManaSymbol ON uniqueManaSymbols(uniqueManaSymbol);",
                        connection))
                    {
                        command.ExecuteNonQuery();
                    }

                    Debug.WriteLine("Created table and index for uniqueManaSymbols.");

                    connection.Close();
                }


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
            downloadProgressWindow.Close();
        }
    }



}
