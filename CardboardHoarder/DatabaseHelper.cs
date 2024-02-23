using CardboardHoarder;
using Microsoft.Extensions.Configuration;
using SkiaSharp;
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
        IConfigurationBuilder builder = new ConfigurationBuilder()
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
        

        try
        {
            // Retrieve the SQLite database path from appsettings.json
            string sqlitePath = GetSQLitePath();
            string databasePath = Path.Combine(sqlitePath, "AllPrintings.sqlite");

            // Check if the database file exists
            if (!File.Exists(databasePath))
            {
                // Create and show the DownloadWindow
                DownloadWindow downloadWindow = new();
                downloadWindow.Show();

                Debug.WriteLine($"The database file '{databasePath}' does not exist.");
                DownloadDatabaseIfNotExists();
                GenerateCustomDbData();

                // Close the DownloadWindow
                downloadWindow.Close();
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
                using (HttpClient httpClient = new())
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
    }
    private static void SetupDatabase(string databasePath)
    {
        // Open the downloaded database
        using (SQLiteConnection connection = GetConnection())
        {
            connection.Open();

            // Define tables to create
            Dictionary<string, string> tables = new()
            {
                { "uniqueManaSymbols", "CREATE TABLE IF NOT EXISTS uniqueManaSymbols (uniqueManaSymbol TEXT PRIMARY KEY, manaSymbolImage BLOB);" },
                { "uniqueManaCostImages", "CREATE TABLE IF NOT EXISTS uniqueManaCostImages (uniqueManaCost TEXT PRIMARY KEY, manaCostImage BLOB);" },
                { "cardImageStrings", "CREATE TABLE IF NOT EXISTS cardImageStrings (uuid VARCHAR(36) PRIMARY KEY, imageLink TEXT);" },
                { "keyruneImages", "CREATE TABLE IF NOT EXISTS keyruneImages (setCode TEXT PRIMARY KEY, keyRuneImage BLOB);" }
            };

            // Create the tables
            foreach (KeyValuePair<string, string> item in tables)
            {
                using (SQLiteCommand command = new(item.Value, connection))
                {
                    command.ExecuteNonQuery();
                }

                Debug.WriteLine($"Created table for {item.Key}.");
            }

            // Define indices to create
            Dictionary<string, string> indices = new()
            {
                { "uniqueManaSymbols", "CREATE INDEX IF NOT EXISTS uniqueManaSymbols_uniqueManaSymbol ON uniqueManaSymbols(uniqueManaSymbol);" },
                { "uniqueManaCostImages", "CREATE INDEX IF NOT EXISTS uniqueManaCostImages_uniqueManaCost ON uniqueManaCostImages(uniqueManaCost);" },
                { "cardImageStrings", "CREATE INDEX IF NOT EXISTS cardImageStrings_uuid ON cardImageStrings(uuid);" },
                { "keyruneImages", "CREATE INDEX IF NOT EXISTS keyruneImages_setCode ON keyruneImages(setCode);" },
            };

            // Create the indices
            foreach (KeyValuePair<string, string> item in indices)
            {
                using (SQLiteCommand command = new(item.Value, connection))
                {
                    command.ExecuteNonQuery();
                }

                Debug.WriteLine($"Created index for {item.Key}.");
            }

            connection.Close();
        }
    }
    #endregion
    private static void GenerateCustomDbData()
    {
        GenerateManaSymbolsFromSvg();
    }


    private static void GenerateManaSymbolsFromSvg()
    {
        try
        {
            List<string> uniqueManaCosts = GetUniqueValues("cards", "manaCost");
            List<string> uniqueSymbols = new();

            foreach (string manaCost in uniqueManaCosts)
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
            foreach (string symbol in uniqueSymbols)
            {
                InsertOrUpdateSymbolInTable(symbol, "uniqueManaSymbols", "uniqueManaSymbol");
            }

            Debug.WriteLine("Insertion of uniqueManaSymbols completed.");

            // Get a list of mana symbols without image
            List<string> symbolsWithNullImage = GetValuesWithNull("uniqueManaSymbols", "uniqueManaSymbol", "manaSymbolImage");

            // Generate the missing mana cost symbols and insert them into table uniqueManaSymbols
            using (SQLiteConnection connection = GetConnection())
            {
                connection.Open();

                foreach (string missingImage in symbolsWithNullImage)
                {
                    Debug.WriteLine($"https://svgs.scryfall.io/card-symbols/{missingImage.Replace("/", "")}.svg");

                    // Convert SVG to PNG using the ConvertSvgToPng function
                    byte[] pngData = ConvertSvgToPng($"https://svgs.scryfall.io/card-symbols/{missingImage.Replace("/", "")}.svg");

                    if (pngData != null)
                    {
                        // Update the 'uniqueManaSymbols' table with the PNG data
                        UpdateImageInTable(missingImage, "uniqueManaSymbols", "manaSymbolImage", pngData);
                    }
                    else
                    {
                        // Handle the case when conversion fails (e.g., log, show error message, etc.)
                        Debug.WriteLine($"Failed to convert SVG to PNG for symbol: {missingImage}");
                    }
                }
                connection.Close();
            }
        }
        catch (Exception ex)
        {
            // Handle exceptions (e.g., log, show error message, etc.)
            Debug.WriteLine($"Error during insertion of uniqueManaSymbols: {ex.Message}");
        }
    }
    private static void UpdateImageInTable(string uniqueManaSymbol, string tableName, string columnName, byte[] imageData)
    {
        try
        {
            using (SQLiteConnection connection = GetConnection())
            {
                connection.Open();

                // Update the existing row with the PNG data
                using (SQLiteCommand command = new SQLiteCommand(
                        $"UPDATE {tableName} SET {columnName} = @imageData WHERE {columnName} IS NULL AND uniqueManaSymbol = @uniqueManaSymbol",
                        connection))
                {
                    command.Parameters.AddWithValue("@uniqueManaSymbol", uniqueManaSymbol);
                    command.Parameters.AddWithValue("@imageData", imageData);
                    command.ExecuteNonQuery();
                }

                connection.Close();
            }
        }
        catch (Exception ex)
        {
            // Handle exceptions (e.g., log, show error message, etc.)
            Debug.WriteLine($"Error while updating image in table: {ex.Message}");
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
                using (SQLiteCommand selectCommand = new(
                    $"SELECT COUNT(*) FROM {tableName} WHERE {columnName} = @symbol",
                    connection))
                {
                    selectCommand.Parameters.AddWithValue("@symbol", symbol);

                    int count = Convert.ToInt32(selectCommand.ExecuteScalar());

                    if (count > 0)
                    {
                        // Symbol exists, perform an update
                        using (SQLiteCommand updateCommand = new(
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
                        using (SQLiteCommand insertCommand = new(
                            $"INSERT INTO {tableName} ({columnName}) VALUES (@symbol)",
                            connection))
                        {
                            insertCommand.Parameters.AddWithValue("@symbol", symbol);
                            insertCommand.ExecuteNonQuery();
                        }
                    }
                }

                connection.Close();
            }
        }
        catch (Exception ex)
        {
            // Handle exceptions (e.g., log, show error message, etc.)
            Debug.WriteLine($"Error during insertion or update: {ex.Message}");
        }
    }
    private static byte[] ConvertSvgToPng(string svgLink)
    {
        try
        {
            string svgContent;

            // Download the SVG content from the link using HttpClient
            using (HttpClient client = new HttpClient())
            {
                svgContent = client.GetStringAsync(svgLink).Result;
            }

            using (var svgStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(svgContent)))
            {
                var svg = new SkiaSharp.Extended.Svg.SKSvg();
                svg.Load(svgStream);

                // Calculate the scaling factor to limit height to 20 pixels
                float scaleFactor = 20f / svg.CanvasSize.Height;

                // Create SKBitmap with adjusted size
                using (var bitmap = new SKBitmap((int)(svg.CanvasSize.Width * scaleFactor), 20))
                {
                    using (var canvas = new SKCanvas(bitmap))
                    {
                        canvas.Clear(SKColors.Transparent);

                        // Apply scaling to the canvas
                        canvas.Scale(scaleFactor);

                        // Draw the SVG picture on the canvas
                        canvas.DrawPicture(svg.Picture);
                    }

                    // Save SKBitmap as PNG to a memory stream
                    using (var image = SKImage.FromBitmap(bitmap))
                    using (var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100))
                    using (var stream = new MemoryStream())
                    {
                        data.SaveTo(stream);
                        return stream.ToArray();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Handle exceptions (e.g., log, show error message, etc.)
            Console.WriteLine($"Error while converting SVG to PNG: {ex.Message}");
            return null; // or throw an exception if you prefer
        }
    }




    private static List<string> GetValuesWithNull(string tableName, string returnColumnName, string searchColumnName)
    {
        List<string> valuesWithNull = new List<string>();

        using (SQLiteConnection connection = GetConnection())
        {
            connection.Open();

            // Retrieve values where specified column is null
            string query = $"SELECT {returnColumnName} FROM {tableName} WHERE {searchColumnName} IS NULL";

            using (SQLiteCommand command = new SQLiteCommand(query, connection))
            {
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string value = reader[returnColumnName].ToString();
                        valuesWithNull.Add(value);
                    }
                }
            }
        }

        return valuesWithNull;
    }
    private static List<string> GetUniqueValues(string tableName, string columnName)
    {
        List<string> uniqueValues = new();

        using (SQLiteConnection connection = GetConnection())
        {
            connection.Open();

            string query = $"SELECT DISTINCT {columnName} FROM {tableName};";

            using (SQLiteCommand command = new(query, connection))
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





