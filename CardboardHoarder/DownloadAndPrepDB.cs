using CardboardHoarder;
using Newtonsoft.Json.Linq;
using SkiaSharp;
using System.Data.SQLite;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Windows;
public class DownloadAndPrepDB
{
    public static event Action<string>? StatusMessageUpdated;
    public static string databasePath = Path.Combine(DBAccess.sqlitePath, "AllPrintings.sqlite");

    /// <summary>
    /// Check if the card database exists in the location specified by appsettings.json. 
    /// If it doesn't download it and populate it with custom data, including image data for mana symbols and set images
    /// </summary>
    public static async Task CheckDatabaseExistenceAsync()
    {
        try
        {


            if (!File.Exists(databasePath))
            {

                // Call the download method with the progress handler
                await DownloadDatabaseIfNotExistsAsync(databasePath);
                await DBAccess.OpenConnectionAsync(true);
                await CreateCustomTablesAndIndices(databasePath);
                await GenerateManaSymbolsFromSvgAsync();
                // Now run the last two functions in parallel
                var generateManaCostImagesTask = GenerateManaCostImagesAsync();
                var generateSetKeyruneFromSvgTask = GenerateSetKeyruneFromSvgAsync();
                await Task.WhenAll(generateManaCostImagesTask, generateSetKeyruneFromSvgTask);

                DBAccess.CloseConnection(true);
                MainWindow.ShowOrHideStatusWindow(false);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error while checking database existence: {ex.Message}");
        }
    }
    /// <summary>
    /// Download card database from mtgjson in SQLite format
    /// </summary>
    public static async Task DownloadDatabaseIfNotExistsAsync(string databasePath)
    {
        try
        {
            MainWindow.ShowOrHideStatusWindow(true);

            IProgress<int> progress = new Progress<int>(value =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (MainWindow.CurrentInstance?.progressBar != null)
                    {
                        MainWindow.CurrentInstance.progressBar.Value = value;
                    }
                });
            });

            string downloadUrl = "https://mtgjson.com/api/v5/AllPrintings.sqlite";
            using (var httpClient = new HttpClient())
            using (var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl))
            using (var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var megabytes = string.Format("{0:0.0} MB", totalBytes / 1000000.0);
                var totalBytesRead = 0L;
                var buffer = new byte[4096];
                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(databasePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
                {
                    StatusMessageUpdated?.Invoke($"Downloading card database ({megabytes})");
                    var bytesRead = 0;
                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalBytesRead += bytesRead;
                        var progressPercentage = totalBytes != -1 ? (int)((totalBytesRead * 100) / totalBytes) : -1;
                        progress?.Report(progressPercentage);
                    }
                }
            }
            Debug.WriteLine($"Download completed. The database file '{databasePath}' is now available.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error during download: {ex.Message}");
            StatusMessageUpdated?.Invoke("Download failed.");
        }
        finally
        {
            if (MainWindow.CurrentInstance?.progressBar != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MainWindow.CurrentInstance.progressBar.Visibility = Visibility.Hidden;
                });
            }
        }
    }

    /// <summary>
    /// Generate custom data such as manasymbols, mana cost, set images and save them as png in database
    /// </summary>
    private static async Task CreateCustomTablesAndIndices(string databasePath)
    {
        try
        {
            StatusMessageUpdated?.Invoke("Creating custom tables and indices");

            // Define tables to create
            Dictionary<string, string> tables = new()
        {
            {"uniqueManaSymbols", "CREATE TABLE IF NOT EXISTS uniqueManaSymbols (uniqueManaSymbol TEXT PRIMARY KEY, manaSymbolImage BLOB);"},
            {"uniqueManaCostImages", "CREATE TABLE IF NOT EXISTS uniqueManaCostImages (uniqueManaCost TEXT PRIMARY KEY, manaCostImage BLOB);"},
            {"cardImageStrings", "CREATE TABLE IF NOT EXISTS cardImageStrings (uuid VARCHAR(36) PRIMARY KEY, imageLink TEXT);"},
            {"keyruneImages", "CREATE TABLE IF NOT EXISTS keyruneImages (setCode TEXT PRIMARY KEY, keyruneImage BLOB);"}
        };

            // Create the tables asynchronously
            foreach (var item in tables)
            {
                using (var command = new SQLiteCommand(item.Value, DBAccess.connection))
                {
                    await command.ExecuteNonQueryAsync();
                    Debug.WriteLine($"Created table for {item.Key}.");
                }
            }

            // Define indices to create
            Dictionary<string, string> indices = new()
        {
            {"uniqueManaSymbols", "CREATE INDEX IF NOT EXISTS uniqueManaSymbols_uniqueManaSymbol ON uniqueManaSymbols(uniqueManaSymbol);"},
            {"uniqueManaCostImages", "CREATE INDEX IF NOT EXISTS uniqueManaCostImages_uniqueManaCost ON uniqueManaCostImages(uniqueManaCost);"},
            {"cardImageStrings", "CREATE INDEX IF NOT EXISTS cardImageStrings_uuid ON cardImageStrings(uuid);"},
            {"keyruneImages", "CREATE INDEX IF NOT EXISTS keyruneImages_setCode ON keyruneImages(setCode);"}
        };

            // Create the indices asynchronously
            foreach (var item in indices)
            {
                using (var command = new SQLiteCommand(item.Value, DBAccess.connection))
                {
                    await command.ExecuteNonQueryAsync();
                    Debug.WriteLine($"Created index for {item.Key}.");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error during creation of tables and indices: {ex.Message}");
        }
    }
    private static async Task GenerateManaSymbolsFromSvgAsync()
    {
        StatusMessageUpdated?.Invoke("Generating mana symbol images");
        try
        {
            List<string> uniqueManaCosts = await GetUniqueValuesAsync("cards", "manaCost");
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
                await InsertValueInTableAsync(symbol, "uniqueManaSymbols", "uniqueManaSymbol");
            }

            Debug.WriteLine("Insertion of uniqueManaSymbols completed.");

            // Get a list of mana symbols without image
            List<string> symbolsWithNullImage = await GetValuesWithNullAsync("uniqueManaSymbols", "uniqueManaSymbol", "manaSymbolImage");

            // Generate the missing mana cost symbols and insert them into table uniqueManaSymbols
            int counter = 0;
            foreach (string missingImage in symbolsWithNullImage)
            {
                counter++;
                // Convert SVG to PNG using the ConvertSvgToPng function
                byte[] pngData = await ConvertSvgToPngAsync($"https://svgs.scryfall.io/card-symbols/{missingImage.Replace("/", "")}.svg");

                if (pngData.Length != 0)
                {
                    // Update the 'uniqueManaSymbols' table with the PNG data
                    await UpdateImageInTableAsync(missingImage, "uniqueManaSymbols", "manaSymbolImage", "uniqueManaSymbol", pngData);
                    StatusMessageUpdated?.Invoke($"Added image generated from https://svgs.scryfall.io/card-symbols/{missingImage.Replace("/", "")}.svg ({counter} out of {symbolsWithNullImage.Count.ToString()})");
                    Debug.WriteLine($"Added image generated from https://svgs.scryfall.io/card-symbols/{missingImage.Replace("/", "")}.svg");
                }
                else
                {
                    // Handle the case when conversion fails (e.g., log, show error message, etc.)
                    Debug.WriteLine($"Failed to convert SVG to PNG for symbol: {missingImage}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error during creation or insertion of uniqueManaSymbols: {ex.Message}");
        }
    }
    private static async Task GenerateManaCostImagesAsync()
    {
        List<string> uniqueManaCosts = await GetUniqueValuesAsync("cards", "manaCost");

        // Insert unique symbols into the 'uniqueManaSymbols' table if it's not already there
        foreach (string manaCost in uniqueManaCosts)
        {
            await InsertValueInTableAsync(manaCost, "uniqueManaCostImages", "uniqueManaCost");
            StatusMessageUpdated?.Invoke($"Added {manaCost} to table");
        }

        List<string> manaCostsWithNullImage = await GetValuesWithNullAsync("uniqueManaCostImages", "uniqueManaCost", "manaCostImage");

        // Generate the missing mana cost images and insert them into table uniqueManaCostImages
        int counter = 0;
        foreach (string missingImage in manaCostsWithNullImage)
        {
            counter++;
            await UpdateImageInTableAsync(missingImage, "uniqueManaCostImages", "manaCostImage", "uniqueManaCost", await ProcessManaCostInputAsync(missingImage));
            StatusMessageUpdated?.Invoke($"Added image for the mana cost {missingImage} ({counter.ToString()} of {manaCostsWithNullImage.Count().ToString()}");
            Debug.WriteLine($"Added image for the mana cost {missingImage} ({counter.ToString()} of {manaCostsWithNullImage.Count().ToString()})");
        }
    }
    private static async Task GenerateSetKeyruneFromSvgAsync()
    {
        try
        {
            // Insert setCode into the 'keyRuneImages' table if it's not already there
            StatusMessageUpdated?.Invoke($"Copying set image references");
            await CopyColumnIfEmptyOrAddMissingRowsAsync("keyruneImages", "setCode", "sets", "code");

            List<string> setCodesWithNoImage = await GetValuesWithNullAsync("keyruneImages", "setCode", "keyruneImage");

            // Initialize an array of List<string> with two elements
            List<string>[] setCodesToGenerateImagesFrom = new List<string>[2];


            // Assign the first position with the list from the database
            setCodesToGenerateImagesFrom[0] = setCodesWithNoImage;
            setCodesToGenerateImagesFrom[1] = new List<string>();

            HttpClient client = new HttpClient();
            string url = $"https://api.scryfall.com/sets/";

            try
            {
                StatusMessageUpdated?.Invoke($"Downloading reference for set icons");
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
                Debug.WriteLine($"An error occurred while trying to get set information from scryfall: {ex.Message}");
            }

            // Generate the missing set images and insert them into table 'keyruneImages'
            for (int i = 0; i < setCodesToGenerateImagesFrom[0].Count; i++)
            {

                string setCode = setCodesToGenerateImagesFrom[0][i];
                string svgUri = setCodesToGenerateImagesFrom[1][i];

                // Convert SVG to PNG using the ConvertSvgToPng function
                byte[] pngData = await ConvertSvgToPngAsync(svgUri);

                if (pngData.Length != 0)
                {
                    // Update the 'uniqueManaSymbols' table with the PNG data
                    await UpdateImageInTableAsync(setCode, "keyruneImages", "keyruneImage", "setCode", pngData);
                    StatusMessageUpdated?.Invoke($"Generated keyruneImage from {svgUri} ({i} of {setCodesToGenerateImagesFrom[0].Count})");
                    Debug.WriteLine($"Generated set icon image from {svgUri}");
                }
                else
                {
                    // Handle the case when conversion fails (e.g., log, show error message, etc.)
                    Debug.WriteLine($"Failed to convert SVG to PNG for symbol: {setCode}");
                }
            }
        }
        catch (Exception ex)
        {
            // Handle exceptions (e.g., log, show error message, etc.)
            Debug.WriteLine($"Error during insertion of keyRuneImages: {ex.Message}");
        }
    }

    #region Helper methods
    private static async Task<byte[]> ProcessManaCostInputAsync(string manaCostInput)
    {
        List<Bitmap> manaSymbolImage = new List<Bitmap>();

        try
        {
            string[] manaSymbols = manaCostInput.Trim(new char[] { '{', '}' }).Split(new string[] { "}{" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string symbol in manaSymbols)
            {
                using (SQLiteCommand command = new SQLiteCommand(
                        $"SELECT manaSymbolImage FROM uniqueManaSymbols WHERE uniqueManaSymbol = @symbol",
                        DBAccess.connection))
                {
                    command.Parameters.AddWithValue("@symbol", symbol);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            byte[] imageBytes = (byte[])reader["manaSymbolImage"];
                            using (MemoryStream ms = new MemoryStream(imageBytes))
                            {
                                Bitmap bitmap = new Bitmap(ms); // Bitmap and SkiaSharp operations are not async
                                manaSymbolImage.Add(bitmap);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"An error occurred while processing mana cost input: {ex.Message}");
        }

        return await CombineImagesAsync(manaSymbolImage);
    }
    private static async Task<byte[]> CombineImagesAsync(List<Bitmap> images)
    {
        return await Task.Run(() => CombineImages(images));
    }
    private static byte[] CombineImages(List<Bitmap> images)
    {
        if (images == null || images.Count == 0)
        {
            throw new ArgumentException("Images list is null or empty", nameof(images));
        }

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
                g.DrawImage(image, new System.Drawing.Point(offset, 0));
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
    private static async Task<byte[]> ConvertSvgToPngAsync(string svgLink)
    {
        try
        {
            string svgContent;
            using (HttpClient client = new HttpClient())
            {
                svgContent = await client.GetStringAsync(svgLink);
            }

            using (var svgStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(svgContent)))
            {
                var svg = new SkiaSharp.Extended.Svg.SKSvg();
                svg.Load(svgStream);
                float scaleFactor = 20f / svg.CanvasSize.Height;

                using (var bitmap = new SKBitmap((int)(svg.CanvasSize.Width * scaleFactor), 20))
                using (var canvas = new SKCanvas(bitmap))
                {
                    canvas.Clear(SKColors.Transparent);
                    canvas.Scale(scaleFactor);
                    canvas.DrawPicture(svg.Picture);

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
            return Array.Empty<byte>();
        }
    }
    private static async Task CopyColumnIfEmptyOrAddMissingRowsAsync(string targetTable, string targetColumn, string sourceTable, string sourceColumn)
    {
        try
        {
            string checkQuery = $"SELECT COUNT(*) FROM {targetTable} WHERE {targetColumn} IS NOT NULL AND {targetColumn} != '';";
            using (var checkCommand = new SQLiteCommand(checkQuery, DBAccess.connection))
            {
                int result = Convert.ToInt32(await checkCommand.ExecuteScalarAsync());

                if (result == 0)
                {
                    string copyQuery = $@"
                        BEGIN TRANSACTION;
                        INSERT OR IGNORE INTO {targetTable} ({targetColumn})
                        SELECT DISTINCT {sourceColumn} FROM {sourceTable};
                        COMMIT;";
                    using (var copyCommand = new SQLiteCommand(copyQuery, DBAccess.connection))
                    {
                        await copyCommand.ExecuteNonQueryAsync();
                        Debug.WriteLine($"Copied all rows from {sourceTable}, column {sourceColumn} to {targetTable}, {targetColumn}");
                    }
                }
                else
                {
                    string copyQuery = $@"
                        INSERT INTO {targetTable} ({targetColumn})
                        SELECT {sourceTable}.{sourceColumn} FROM {sourceTable}
                        LEFT JOIN {targetTable} ON {sourceTable}.{sourceColumn} = {targetTable}.{targetColumn}
                        WHERE {targetTable}.{targetColumn} IS NULL;";
                    using (var command = new SQLiteCommand(copyQuery, DBAccess.connection))
                    {
                        await command.ExecuteNonQueryAsync();
                    }
                    Debug.WriteLine($"Updated missing rows in {targetTable}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("An error occurred: " + ex.Message);
        }
    }
    private static async Task UpdateImageInTableAsync(string imageToUpdate, string tableName, string columnToUpdate, string columnToReference, byte[] imageData)
    {
        try
        {
            using (var command = new SQLiteCommand(
                            $"UPDATE {tableName} SET {columnToUpdate} = @imageData WHERE {columnToUpdate} IS NULL AND {columnToReference} = @referenceColumn",
                            DBAccess.connection))
            {
                command.Parameters.AddWithValue("@referenceColumn", imageToUpdate);
                command.Parameters.AddWithValue("@imageData", imageData);
                await command.ExecuteNonQueryAsync();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error while updating image in table: {ex.Message}");
        }
    }
    private static async Task InsertValueInTableAsync(string value, string tableName, string columnName)
    {
        try
        {
            using (var selectCommand = new SQLiteCommand(
                $"SELECT COUNT(*) FROM {tableName} WHERE {columnName} = @value", DBAccess.connection))
            {
                selectCommand.Parameters.AddWithValue("@value", value);
                var count = Convert.ToInt32(await selectCommand.ExecuteScalarAsync());

                if (count == 0)
                {
                    using (var insertCommand = new SQLiteCommand(
                        $"INSERT INTO {tableName} ({columnName}) VALUES (@value)", DBAccess.connection))
                    {
                        insertCommand.Parameters.AddWithValue("@value", value);
                        await insertCommand.ExecuteNonQueryAsync();
                        Debug.WriteLine($"Added {value} to the table");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error during insertion: {ex.Message}");
        }
    }
    private static async Task<List<string>> GetValuesWithNullAsync(string tableName, string returnColumnName, string searchColumnName)
    {
        List<string> valuesWithNull = new List<string>();
        try
        {
            string query = $"SELECT {returnColumnName} FROM {tableName} WHERE {searchColumnName} IS NULL";
            using (var command = new SQLiteCommand(query, DBAccess.connection))
            {
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        string value = reader[returnColumnName]?.ToString() ?? string.Empty;
                        valuesWithNull.Add(value);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error retrieving values with null: {ex.Message}");
        }
        return valuesWithNull;
    }
    private static async Task<List<string>> GetUniqueValuesAsync(string tableName, string columnName)
    {
        List<string> uniqueValues = new List<string>();

        try
        {
            string query = $"SELECT DISTINCT {columnName} FROM {tableName};";

            using (var command = new SQLiteCommand(query, DBAccess.connection))
            {
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        string value = reader[columnName]?.ToString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(value))
                        {
                            uniqueValues.Add(value);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"An error occurred while fetching unique values: {ex.Message}");
        }

        return uniqueValues;
    }
    #endregion
}





