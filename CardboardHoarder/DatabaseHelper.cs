using CardboardHoarder;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using SkiaSharp;
using System.Data.SQLite;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
public class DatabaseHelper
{
    private static string _sqlitePath = string.Empty; 
    private static string sqlitePath
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
    public static void CheckDatabaseExistence()
    {
        try
        {
            // Retrieve the SQLite database path from appsettings.json
            string databasePath = Path.Combine(sqlitePath, "AllPrintings.sqlite");

            // Check if the database file exists
            if (!File.Exists(databasePath))
            {
                // Create and show the DownloadWindow
                DownloadWindow downloadWindow = new();
                downloadWindow.Show();

                Debug.WriteLine($"The database file '{databasePath}' does not exist.");
                DownloadDatabaseIfNotExists(databasePath);
                SetupDatabase(databasePath);
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
    public static void DownloadDatabaseIfNotExists(string databasePath)
    {
        try
        {
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
                { "keyruneImages", "CREATE TABLE IF NOT EXISTS keyruneImages (setCode TEXT PRIMARY KEY, keyruneImage BLOB);" }
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
        GenerateManaCostImages();
        GenerateSetKeyruneFromSvg();
        
    }
    // Generates a mana cost symbol from svg retrieved from scryfall weblink
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
                InsertValueInTable(symbol, "uniqueManaSymbols", "uniqueManaSymbol");
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

                    // Convert SVG to PNG using the ConvertSvgToPng function
                    byte[] pngData = ConvertSvgToPng($"https://svgs.scryfall.io/card-symbols/{missingImage.Replace("/", "")}.svg");

                    if (pngData.Length != 0)
                    {
                        // Update the 'uniqueManaSymbols' table with the PNG data
                        UpdateImageInTable(missingImage, "uniqueManaSymbols", "manaSymbolImage", "uniqueManaSymbol", pngData);
                        Debug.WriteLine($"Added image generated from https://svgs.scryfall.io/card-symbols/{missingImage.Replace("/", "")}.svg");
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
    // Creates a list of bitmaps for a single mana cost
    private static void GenerateManaCostImages()
    {
        List<string> uniqueManaCosts = GetUniqueValues("cards", "manaCost");

        // Insert unique symbols into the 'uniqueManaSymbols' table if it's not already there
        foreach (string manaCost in uniqueManaCosts)
        {
            InsertValueInTable(manaCost, "uniqueManaCostImages", "uniqueManaCost");
        }

        List<string> manaCostsWithNullImage = GetValuesWithNull("uniqueManaCostImages", "uniqueManaCost", "manaCostImage");

        // Generate the missing mana cost images and insert them into table uniqueManaCostImages
        using (SQLiteConnection connection = GetConnection())
        {
            connection.Open();

            foreach (string missingImage in manaCostsWithNullImage)
            {
                UpdateImageInTable(missingImage, "uniqueManaCostImages", "manaCostImage", "uniqueManaCost", ProcessManaCostInput(missingImage));
                Debug.WriteLine($"Added image for the mana cost {missingImage}");
            }
            connection.Close();
        }
    }
    #region Helper functions for GenerateManaCostImages()
    private static byte[] ProcessManaCostInput(string manaCostInput)
    {
        string[] manaSymbols = manaCostInput.Trim(new char[] { '{', '}' }).Split(new string[] { "}{" }, StringSplitOptions.RemoveEmptyEntries);
        List<Bitmap> manaSymbolImage = new List<Bitmap>();

        foreach (string symbol in manaSymbols)
        {
            using (SQLiteConnection connection = GetConnection())
            {
                connection.Open();

                using (SQLiteCommand command = new SQLiteCommand(
                        $"SELECT manaSymbolImage FROM uniqueManaSymbols WHERE uniqueManaSymbol = @symbol",
                        connection))
                {
                    command.Parameters.AddWithValue("@symbol", symbol);

                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            byte[] imageBytes = (byte[])reader["manaSymbolImage"];
                            using (MemoryStream ms = new MemoryStream(imageBytes))
                            {
                                Bitmap bitmap = new Bitmap(ms);
                                manaSymbolImage.Add(bitmap);
                            }
                        }
                    }
                }
                connection.Close();
            }
        }

        return CombineImages(manaSymbolImage);
    }
    // Combine list of mana cost bitmaps into a single png
    private static byte[] CombineImages(List<Bitmap> images)
    {
        if (images == null || images.Count == 0)
            throw new ArgumentException("Images list is null or empty", nameof(images));

        int width = images.Sum(img => img.Width);
        int height = images.Max(img => img.Height);

        // Create a new bitmap with the total width and maximum height
        using (Bitmap combinedImage = new Bitmap(width, height))
        using (Graphics g = Graphics.FromImage(combinedImage))
        {
            g.Clear(Color.Transparent); // Optional: fill background if needed

            int offset = 0;
            foreach (Bitmap image in images)
            {
                g.DrawImage(image, new Point(offset, 0));
                offset += image.Width;
                image.Dispose(); // Dispose each image after drawing it
            }

            using (MemoryStream ms = new MemoryStream())
            {
                combinedImage.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                return ms.ToArray();
            }
        }
    }
    #endregion
    // Generate set icon image
    private static void GenerateSetKeyruneFromSvg()
    {
        try
        {            
            // Insert setCode into the 'keyRuneImages' table if it's not already there
            CopyColumnIfEmptyOrAddMissingRows("keyruneImages", "setCode", "sets", "code");

            List<string> setCodesWithNoImage = GetValuesWithNull("keyruneImages", "setCode", "keyruneImage");

            // Initialize an array of List<string> with two elements
            List<string>[] setCodesToGenerateImagesFrom = new List<string>[2];


            // Assign the first position with the list from the database
            setCodesToGenerateImagesFrom[0] = setCodesWithNoImage;
            setCodesToGenerateImagesFrom[1] = new List<string>();

            HttpClient client = new HttpClient();
            string url = $"https://api.scryfall.com/sets/";

            try
            {
                // Synchronously make a request to get all sets
                HttpResponseMessage response = client.GetAsync(url).Result; // Synchronous call, use with caution
                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = response.Content.ReadAsStringAsync().Result; // Synchronous call, use with caution
                    JObject allSets = JObject.Parse(jsonResponse);
                    JArray data = (JArray)allSets["data"];

                    foreach (string setCode in setCodesWithNoImage)
                    {
                        var matchingSet = data!.FirstOrDefault(x => x["code"]?.ToString().Equals(setCode, StringComparison.OrdinalIgnoreCase) == true);

                        if (matchingSet != null)
                        {
                            // Found a matching set, extract the SVG URI
                            string svgUri = matchingSet["icon_svg_uri"]?.ToString() ?? "https://svgs.scryfall.io/sets/default.svg";
                            setCodesToGenerateImagesFrom[1].Add(svgUri); // Add the found SVG URI to the list
                        }
                        else
                        {
                            // No matching set found, use the default SVG URI
                            string defaultSvgUri = "https://svgs.scryfall.io/sets/default.svg";
                            setCodesToGenerateImagesFrom[1].Add(defaultSvgUri); // Add the default SVG URI to the list
                            Debug.WriteLine($"No matching setCode. Added {defaultSvgUri} to array");
                        }
                    }
                }
                else
                {
                    Debug.WriteLine("Failed to retrieve set information from Scryfall.");
                }
            }
            catch (Exception ex)
            {
                // Handle any errors that occur during the request or processing
                Debug.WriteLine($"An error occurred: {ex.Message}");
            }

            // Generate the missing set images and insert them into table 'keyruneImages'
            using (SQLiteConnection connection = GetConnection())
            {
                connection.Open();

                for (int i = 0; i < setCodesToGenerateImagesFrom[0].Count; i++)
                {

                    string setCode = setCodesToGenerateImagesFrom[0][i];
                    string svgUri = setCodesToGenerateImagesFrom[1][i]; 

                    // Convert SVG to PNG using the ConvertSvgToPng function
                    byte[] pngData = ConvertSvgToPng(svgUri);

                    if (pngData.Length != 0)
                    {
                        // Update the 'uniqueManaSymbols' table with the PNG data
                        UpdateImageInTable(setCode, "keyruneImages", "keyruneImage", "setCode", pngData);
                        Debug.WriteLine($"Generated keyruneImage from {svgUri}");
                    }
                    else
                    {
                        // Handle the case when conversion fails (e.g., log, show error message, etc.)
                        Debug.WriteLine($"Failed to convert SVG to PNG for symbol: {setCode}");
                    }                
                }
                connection.Close();
            }
        }
        catch (Exception ex)
        {
            // Handle exceptions (e.g., log, show error message, etc.)
            Debug.WriteLine($"Error during insertion of keyRuneImages: {ex.Message}");
        }
    }



    private static void CopyColumnIfEmptyOrAddMissingRows(string targetTable, string targetColumn, string sourceTable, string sourceColumn)
    {
        try
        {
            using (SQLiteConnection connection = GetConnection())
            {
                connection.Open();

                // Check if all values in targetTableColumn are null or empty
                string checkQuery = $"SELECT COUNT(*) FROM {targetTable} WHERE {targetColumn} IS NOT NULL AND {targetColumn} != '';";

                using (SQLiteCommand checkCommand = new SQLiteCommand(checkQuery, connection))
                {
                    int result = Convert.ToInt32(checkCommand.ExecuteScalar());

                    // If result is 0, it means all rows are null or empty
                    if (result == 0)
                    {
                        // Generate the SQL command to copy data from source to target
                        string copyQuery = $@"
                            BEGIN TRANSACTION;
                            INSERT OR IGNORE INTO {targetTable} ({targetColumn})
                            SELECT DISTINCT {sourceColumn} FROM {sourceTable};
                            COMMIT;";

                        using (SQLiteCommand copyCommand = new SQLiteCommand(copyQuery, connection))
                        {
                            // Execute the query
                            copyCommand.ExecuteNonQuery();
                            Debug.WriteLine($"Copied all rows from {sourceTable}, column {sourceColumn} to {targetTable}, {targetColumn}");
                        }
                    }
                    // If it is not empty, copy any rows that are missing from targetTable from sourceTable
                    else 
                    {
                        string copyQuery = $@"
                            INSERT INTO {targetTable} ({targetColumn})
                            SELECT {sourceTable}.{sourceColumn} FROM {sourceTable}
                            LEFT JOIN {targetTable} ON {sourceTable}.{sourceColumn} = {targetTable}.{targetColumn}
                            WHERE {targetTable}.{targetColumn} IS NULL;
                            ";
                                               
                            using (SQLiteCommand command = new SQLiteCommand(copyQuery, connection))
                            {
                                command.ExecuteNonQuery();
                            }

                        Debug.WriteLine($"Updated missing rows in {targetTable}");
                    }
                }

                connection.Close();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("An error occurred: " + ex.Message);
        }
    }


    #region Toolbox - functions used by multiple custom data functions
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
                using (var canvas = new SKCanvas(bitmap))
                {
                    canvas.Clear(SKColors.Transparent);
                    canvas.Scale(scaleFactor); // Apply scaling
                    canvas.DrawPicture(svg.Picture); // Draw SVG

                    // Save SKBitmap as PNG
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
            Console.WriteLine($"Error while converting SVG to PNG: {ex.Message}");
            return Array.Empty<byte>(); // Return an empty byte array instead of null
        }
    }
    private static void UpdateImageInTable(string imageToUpdate, string tableName, string columnToUpdate, string columnToReference, byte[] imageData)
    {
        try
        {
            using (SQLiteConnection connection = GetConnection())
            {
                connection.Open();

                // Update the existing row with the PNG data
                using (SQLiteCommand command = new SQLiteCommand(
                        $"UPDATE {tableName} SET {columnToUpdate} = @imageData WHERE {columnToUpdate} IS NULL AND {columnToReference} = @referenceColumn",
                        connection))
                {                    
                    command.Parameters.AddWithValue("@referenceColumn", imageToUpdate);
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
    private static void InsertValueInTable(string value, string tableName, string columnName)
    {
        try
        {
            using (SQLiteConnection connection = GetConnection())
            {
                connection.Open();

                // Check if the value already exists in the table
                using (SQLiteCommand selectCommand = new(
                    $"SELECT COUNT(*) FROM {tableName} WHERE {columnName} = @value",
                    connection))
                {
                    selectCommand.Parameters.AddWithValue("@value", value);

                    int count = Convert.ToInt32(selectCommand.ExecuteScalar());

                    if (count == 0)
                    {
                        // Value doesn't exist, perform an insert
                        using (SQLiteCommand insertCommand = new(
                            $"INSERT INTO {tableName} ({columnName}) VALUES (@value)",
                            connection))
                        {
                            insertCommand.Parameters.AddWithValue("@value", value);
                            insertCommand.ExecuteNonQuery();
                            Debug.WriteLine($"Added {value} to the table");
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
                        // Check for null and use an empty string as a fallback
                        string value = reader[returnColumnName]?.ToString() ?? string.Empty;
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
                        // Ensures value is never null, avoiding CS8600 warning
                        string value = reader[columnName]?.ToString() ?? string.Empty;
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
    #endregion
}





