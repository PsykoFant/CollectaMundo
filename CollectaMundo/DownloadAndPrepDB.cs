using Newtonsoft.Json.Linq;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;
using System.Data.SQLite;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CollectaMundo
{
    public class DownloadAndPrepDB
    {
        public static event Action<string>? StatusMessageUpdated;
        private static readonly string databasePath = Path.Combine(DBAccess.SqlitePath, "AllPrintings.sqlite");

        // Check if the card database exists in the location specified by appsettings.json. 
        // If it doesn't exist, download it and populate it with custom data, including image data for mana symbols and set images
        public static async Task SystemIntegrityCheckAsync()
        {
            bool redownloadDB = false;
            string downloadMessage = string.Empty;

            // Check if card database exists            
            if (!File.Exists(databasePath))
            {
                redownloadDB = true; // Set reload-bool to true
                downloadMessage = "Performing first-time setup of card database - please wait...";
            }
            // If it does, check that card database is not corrupt
            else
            {
                if (!await DBAccess.CheckDatabaseIntegrityAsync())
                {
                    // If card database is corrupted, delete corrupt carddatabase. 
                    File.Delete(databasePath);
                    redownloadDB = true; // Set reload-bool to true
                    downloadMessage = "Card database was corrupted! Re-downloading - please wait...";
                }
            }

            if (redownloadDB)
            {
                var downloadDatabaseTask = DownloadResourceFileIfNotExistAsync(databasePath, MainWindow.cardDbDownloadUrl, downloadMessage, "card database", true);
                var downloadPricesTask = DownloadResourceFileIfNotExistAsync(MainWindow.priceDownloadsPath, MainWindow.pricesDownloadUrl, downloadMessage, "", false);

                // Wait for both tasks to complete using Task.WhenAll
                bool[] results = await Task.WhenAll(downloadDatabaseTask, downloadPricesTask);

                // Check if both returned true
                if (results[0] && results[1])
                {
                    await PrepareDownloadedCardDatabase();
                }
                else
                {
                    await SystemIntegrityCheckAsync();
                }
            }
            else
            {
                MainWindow.CurrentInstance.GridContentSection.Visibility = Visibility.Visible;
            }
        }

        // Download card database from mtgjson in SQLite format
        public static async Task<bool> DownloadResourceFileIfNotExistAsync(string downloadTargetPath, string downloadUrl, string statusMessageBig, string downloadFile, bool showStatusBar)
        {
            try
            {
                MainWindow.CurrentInstance.FirstTimeSetupLabel.Content = statusMessageBig;

                if (showStatusBar && MainWindow.CurrentInstance?.ProgressBar != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MainWindow.CurrentInstance.ProgressBar.Visibility = Visibility.Visible;
                    });
                }

                IProgress<int> progress = new Progress<int>(value =>
                {
                    if (showStatusBar)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (MainWindow.CurrentInstance?.ProgressBar != null)
                            {
                                MainWindow.CurrentInstance.ProgressBar.Value = value;
                            }
                        });
                    }
                });

                using var httpClient = new HttpClient();
                using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var totalBytesRead = 0L;
                var buffer = new byte[4096];
                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(downloadTargetPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);

                var megabytes = string.Format("{0:0.0} MB", totalBytes / 1000000.0);
                StatusMessageUpdated?.Invoke($"Downloading {downloadFile} ({megabytes})");

                var bytesRead = 0;
                while ((bytesRead = await contentStream.ReadAsync(buffer)) != 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    totalBytesRead += bytesRead;
                    var progressPercentage = totalBytes != -1 ? (int)((totalBytesRead * 100) / totalBytes) : -1;
                    progress?.Report(progressPercentage);
                }

                Debug.WriteLine($"Download completed. The file '{downloadTargetPath}' is now available.");
                return true; // Return true if download completes successfully
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during download: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Debug.WriteLine($"Error during download: {ex.Message}");
                return false; // Return false in case of any exception
            }
            finally
            {
                if (showStatusBar && MainWindow.CurrentInstance?.ProgressBar != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MainWindow.CurrentInstance.FirstTimeSetupLabel.Content = string.Empty;
                        MainWindow.CurrentInstance.ProgressBar.Visibility = Visibility.Collapsed;
                    });
                }
            }
        }


        // Generate custom data such as manasymbols, mana cost, set images and save them as png in database
        public static async Task PrepareDownloadedCardDatabase()
        {
            await DBAccess.OpenConnectionAsync();

            await Task.Run(CreateCustomTablesAndIndices);

            await GenerateManaSymbolsFromSvgAsync();
            // Now run the last two functions in parallel
            var generateManaCostImagesTask = GenerateManaCostImagesAsync();
            var generateSetKeyruneFromSvgTask = GenerateSetKeyruneFromSvgAsync();
            await Task.WhenAll(generateManaCostImagesTask, generateSetKeyruneFromSvgTask);

            DBAccess.CloseConnection();
        }
        private static async Task CreateCustomTablesAndIndices()
        {
            try
            {
                StatusMessageUpdated?.Invoke("Creating custom tables and indices...");

                // Define tables to create
                Dictionary<string, string> tables = new()
                {
                    {"uniqueManaSymbols", "CREATE TABLE IF NOT EXISTS uniqueManaSymbols (uniqueManaSymbol TEXT PRIMARY KEY, manaSymbolImage BLOB);"},
                    {"uniqueManaCostImages", "CREATE TABLE IF NOT EXISTS uniqueManaCostImages (uniqueManaCost TEXT PRIMARY KEY, manaCostImage BLOB);"},
                    {"keyruneImages", "CREATE TABLE IF NOT EXISTS keyruneImages (setCode TEXT PRIMARY KEY, keyruneImage BLOB);"},
                    {"AggregatedCardKeywords", "CREATE TABLE IF NOT EXISTS AggregatedCardKeywords (uuid TEXT PRIMARY KEY, aggregatedKeywords TEXT);"},
                    {"myCollection", "CREATE TABLE IF NOT EXISTS myCollection (id INTEGER PRIMARY KEY AUTOINCREMENT, uuid TEXT, count INTEGER, trade INTEGER, condition TEXT, language TEXT, finish TEXT);"},
                };

                // Create the tables asynchronously
                foreach (var item in tables)
                {
                    using var command = new SQLiteCommand(item.Value, DBAccess.connection);
                    await command.ExecuteNonQueryAsync();
                }

                // Create indices
                await CreateIndices();

                // Create the view
                await CreateViews();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during creation of tables: {ex.Message}");
                MessageBox.Show($"Error during creation of tables: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        public static async Task GenerateManaSymbolsFromSvgAsync()
        {
            StatusMessageUpdated?.Invoke("Generating mana symbol images...");
            try
            {
                List<string> uniqueManaCosts = await GetUniqueValuesAsync("cards", "manaCost");
                HashSet<string> uniqueSymbols = [];

                // Extract unique symbols in parallel
                uniqueManaCosts.AsParallel().ForAll(manaCost =>
                {
                    MatchCollection matches = Regex.Matches(manaCost, @"\{(.*?)\}");
                    foreach (Match match in matches)
                    {
                        string value = match.Groups[1].Value;
                        lock (uniqueSymbols)
                        {
                            uniqueSymbols.Add(value);
                        }
                    }
                });

                // Batch insert unique symbols into the database
                await Task.WhenAll(uniqueSymbols.Select(symbol => InsertValueInTableAsync(symbol, "uniqueManaSymbols", "uniqueManaSymbol")));

                Debug.WriteLine("Insertion of uniqueManaSymbols completed.");

                // Get a list of mana symbols without image
                List<string> symbolsWithNullImage = await GetValuesWithNullAsync("uniqueManaSymbols", "uniqueManaSymbol", "manaSymbolImage");

                // Parallel generation of missing mana cost symbols and batch update
                var results = await Task.WhenAll(symbolsWithNullImage.Select(async symbol =>
                {
                    byte[] pngData = await ConvertSvgToByteArraySharpVectorsAsync($"https://svgs.scryfall.io/card-symbols/{symbol.Replace("/", "")}.svg");
                    return new { Symbol = symbol, PngData = pngData };
                }));

                foreach (var result in results)
                {
                    if (result.PngData.Length != 0)
                    {
                        await UpdateImageInTableAsync(result.Symbol, "uniqueManaSymbols", "manaSymbolImage", "uniqueManaSymbol", result.PngData);
                    }
                    else
                    {
                        Debug.WriteLine($"Failed to convert SVG to PNG for symbol: {result.Symbol}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during creation or insertion of uniqueManaSymbols: {ex.Message}");
                MessageBox.Show($"Error during creation or insertion of uniqueManaSymbols: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        public static async Task GenerateManaCostImagesAsync()
        {
            try
            {
                List<string> uniqueManaCosts = await GetUniqueValuesAsync("cards", "manaCost");

                // Insert unique symbols into the 'uniqueManaSymbols' table if it's not already there
                var insertTasks = uniqueManaCosts.Select(manaCost =>
                    InsertValueInTableAsync(manaCost, "uniqueManaCostImages", "uniqueManaCost")
                ).ToList();

                await Task.WhenAll(insertTasks);

                List<string> manaCostsWithNullImage = await GetValuesWithNullAsync("uniqueManaCostImages", "uniqueManaCost", "manaCostImage");

                // Generate the missing mana cost images and insert them into table uniqueManaCostImages using parallel processing
                var updateTasks = manaCostsWithNullImage.Select(async (manaCost, index) =>
                {
                    byte[] imageData = await ProcessManaCostInputAsync(manaCost);
                    await UpdateImageInTableAsync(manaCost, "uniqueManaCostImages", "manaCostImage", "uniqueManaCost", imageData);
                }).ToList();

                await Task.WhenAll(updateTasks);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during generation of mana cost images: {ex.Message}");
                MessageBox.Show($"Error during generation of mana cost images: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        public static async Task GenerateSetKeyruneFromSvgAsync()
        {
            StatusMessageUpdated?.Invoke("Generating set icons ...");
            try
            {
                await CopyColumnIfEmptyOrAddMissingRowsAsync("keyruneImages", "setCode", "sets", "code");
                await CopyColumnIfEmptyOrAddMissingRowsAsync("keyruneImages", "setCode", "sets", "tokenSetCode");
                List<string> setCodesWithNoImage = await GetValuesWithNullAsync("keyruneImages", "setCode", "keyruneImage");

                HttpClient client = new();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Your User-Agent Here");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");

                string url = "https://api.scryfall.com/sets/";
                HttpResponseMessage response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"API Error: {responseContent}");
                    return;
                }

                string jsonResponse = await response.Content.ReadAsStringAsync();
                JObject allSets = JObject.Parse(jsonResponse);
                JArray? data = allSets["data"] as JArray;

                var tasks = setCodesWithNoImage.Select(async setCode =>
                {
                    var matchingSet = data?.FirstOrDefault(x => x["code"]?.ToString().Equals(setCode, StringComparison.OrdinalIgnoreCase) == true);
                    string svgUri = matchingSet?["icon_svg_uri"]?.ToString() ?? "https://svgs.scryfall.io/sets/default.svg";
                    byte[] pngData = await ConvertSvgToByteArraySharpVectorsAsync(svgUri);
                    return new { SetCode = setCode, PngData = pngData };
                }).ToList();

                // Await all conversion tasks
                var results = await Task.WhenAll(tasks);

                // Perform batch update to the database
                foreach (var result in results)
                {
                    if (result.PngData.Length != 0)
                    {
                        await UpdateImageInTableAsync(result.SetCode, "keyruneImages", "keyruneImage", "setCode", result.PngData);
                    }
                    else
                    {
                        Debug.WriteLine($"Failed to convert SVG to PNG for symbol: {result.SetCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during insertion of keyRuneImages: {ex.Message}");
                MessageBox.Show($"Error during insertion of keyRuneImages: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region Helper methods
        private static async Task<byte[]> ProcessManaCostInputAsync(string manaCostInput)
        {
            List<Bitmap> manaSymbolImage = [];

            try
            {
                string[] manaSymbols = manaCostInput.Trim(new char[] { '{', '}' }).Split(new string[] { "}{" }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string symbol in manaSymbols)
                {
                    using SQLiteCommand command = new(
                            $"SELECT manaSymbolImage FROM uniqueManaSymbols WHERE uniqueManaSymbol = @symbol",
                            DBAccess.connection);
                    command.Parameters.AddWithValue("@symbol", symbol);

                    using var reader = await command.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        byte[] imageBytes = (byte[])reader["manaSymbolImage"];
                        using MemoryStream ms = new(imageBytes);
                        Bitmap bitmap = new(ms); // Bitmap and SkiaSharp operations are not async
                        manaSymbolImage.Add(bitmap);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"An error occurred while processing mana cost input: {ex.Message}");
                MessageBox.Show($"An error occurred while processing mana cost input: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return await CombineImagesAsync(manaSymbolImage);
        }
        private static async Task<byte[]> CombineImagesAsync(List<Bitmap> images)
        {
            return await Task.Run(() => CombineImages(images));
        }
        public static byte[] CombineImages(List<Bitmap> images)
        {
            try
            {
                if (images == null || images.Count == 0)
                {
                    throw new ArgumentException("Images list is null or empty");
                }

                int totalWidth = 0;
                int maxHeight = 0;

                // Calculate total width and maximum height
                foreach (var image in images)
                {
                    totalWidth += image.Width;
                    if (image.Height > maxHeight)
                    {
                        maxHeight = image.Height;
                    }
                }

                // Check if there's at least one image to reference DPI and pixel format
                if (images.Count > 0)
                {
                    var firstImage = images[0];
                    // Create a new bitmap with matching DPI and pixel format
                    using var combinedImage = new Bitmap(totalWidth, maxHeight, firstImage.PixelFormat);
                    combinedImage.SetResolution(firstImage.HorizontalResolution, firstImage.VerticalResolution);

                    using (var g = Graphics.FromImage(combinedImage))
                    {
                        // Set high-quality rendering options
                        g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

                        // Draw each image side by side
                        int offset = 0;
                        foreach (var image in images)
                        {
                            g.DrawImage(image, new System.Drawing.Point(offset, 0));
                            offset += image.Width;
                        }
                    }

                    // Convert the combined image to a byte array
                    using var ms = new MemoryStream();
                    combinedImage.Save(ms, ImageFormat.Png);
                    return ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"An error occurred while combining mana cost images: {ex.Message}");
                MessageBox.Show($"An error occurred while combining mana cost images: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // Return an empty array failure
            return [];
        }
        public static async Task<byte[]> ConvertSvgToByteArraySharpVectorsAsync(string svgUrl)
        {
            try
            {
                using var httpClient = new HttpClient();
                var svgData = await httpClient.GetStringAsync(svgUrl);
                var svgStream = new MemoryStream(Encoding.UTF8.GetBytes(svgData));
                var settings = new WpfDrawingSettings
                {
                    IncludeRuntime = false,
                    TextAsGeometry = false,
                    OptimizePath = true,
                };
                var reader = new FileSvgReader(settings);
                var drawing = reader.Read(svgStream);

                DrawingImage drawingImage = new(drawing);
                var drawingVisual = new DrawingVisual();
                double aspectRatio = drawingImage.Width / drawingImage.Height;
                int newHeight = 20;
                int newWidth = (int)(newHeight * aspectRatio);

                using (var drawingContext = drawingVisual.RenderOpen())
                {
                    drawingContext.DrawImage(drawingImage, new Rect(0, 0, newWidth, newHeight));
                }
                RenderTargetBitmap renderTargetBitmap = new(newWidth, newHeight, 96, 96, PixelFormats.Pbgra32);
                renderTargetBitmap.Render(drawingVisual);

                System.Windows.Media.Imaging.BitmapEncoder encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(renderTargetBitmap));

                using MemoryStream memoryStream = new();
                encoder.Save(memoryStream);
                return memoryStream.ToArray();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error converting SVG to byte array: {ex.Message}");
                MessageBox.Show($"Error converting SVG to byte array: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return [];
            }
        }
        private static async Task CopyColumnIfEmptyOrAddMissingRowsAsync(string targetTable, string targetColumn, string sourceTable, string sourceColumn)
        {
            try
            {
                string checkQuery = $"SELECT COUNT(*) FROM {targetTable} WHERE {targetColumn} IS NOT NULL AND {targetColumn} != '';";
                using var checkCommand = new SQLiteCommand(checkQuery, DBAccess.connection);
                int result = Convert.ToInt32(await checkCommand.ExecuteScalarAsync());

                if (result == 0)
                {
                    string copyQuery = $@"
                        BEGIN TRANSACTION;
                        INSERT OR IGNORE INTO {targetTable} ({targetColumn})
                        SELECT DISTINCT {sourceColumn} FROM {sourceTable};
                        COMMIT;";
                    using var copyCommand = new SQLiteCommand(copyQuery, DBAccess.connection);
                    await copyCommand.ExecuteNonQueryAsync();
                    Debug.WriteLine($"Copied all rows from {sourceTable}, column {sourceColumn} to {targetTable}, {targetColumn}");
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
            catch (Exception ex)
            {
                Debug.WriteLine("An error occurred while copying emty or missing rows: " + ex.Message);
                MessageBox.Show($"An error occurred while copying emty or missing rows: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

            }
        }
        private static async Task UpdateImageInTableAsync(string imageToUpdate, string tableName, string columnToUpdate, string columnToReference, byte[] imageData)
        {
            try
            {
                using var command = new SQLiteCommand($"UPDATE {tableName} SET {columnToUpdate} = @imageData WHERE {columnToUpdate} IS NULL AND {columnToReference} = @referenceColumn", DBAccess.connection);
                command.Parameters.AddWithValue("@referenceColumn", imageToUpdate);
                command.Parameters.AddWithValue("@imageData", imageData);
                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while updating image in table: {ex.Message}");
                MessageBox.Show($"Error while updating image in table: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private static async Task InsertValueInTableAsync(string value, string tableName, string columnName)
        {
            try
            {
                using var selectCommand = new SQLiteCommand($"SELECT COUNT(*) FROM {tableName} WHERE {columnName} = @value", DBAccess.connection);
                selectCommand.Parameters.AddWithValue("@value", value);
                var count = Convert.ToInt32(await selectCommand.ExecuteScalarAsync());

                if (count == 0)
                {
                    using var insertCommand = new SQLiteCommand(
                        $"INSERT INTO {tableName} ({columnName}) VALUES (@value)", DBAccess.connection);
                    insertCommand.Parameters.AddWithValue("@value", value);
                    await insertCommand.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during insertion of values into table: {ex.Message}");
                MessageBox.Show($"Error during insertion of values into table: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private static async Task<List<string>> GetValuesWithNullAsync(string tableName, string returnColumnName, string searchColumnName)
        {
            List<string> valuesWithNull = [];
            try
            {
                string query = $"SELECT {returnColumnName} FROM {tableName} WHERE {searchColumnName} IS NULL";
                using var command = new SQLiteCommand(query, DBAccess.connection);
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string value = reader[returnColumnName]?.ToString() ?? string.Empty;
                    valuesWithNull.Add(value);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error retrieving values with null: {ex.Message}");
                MessageBox.Show($"Error retrieving values with null: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return valuesWithNull;
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
        public static async Task CreateIndices()
        {
            Dictionary<string, string> indices = new()
            {
                {"uniqueManaSymbols", "CREATE INDEX IF NOT EXISTS uniqueManaSymbols_uniqueManaSymbol ON uniqueManaSymbols(uniqueManaSymbol);"},
                {"uniqueManaCostImages", "CREATE INDEX IF NOT EXISTS uniqueManaCostImages_uniqueManaCost ON uniqueManaCostImages(uniqueManaCost);"},
                {"keyruneImages", "CREATE INDEX IF NOT EXISTS keyruneImages_setCode ON keyruneImages(setCode);"},
                {"cardForeignData", "CREATE INDEX IF NOT EXISTS cardForeignData_uuid ON cardForeignData(uuid);"},
                {"cardIdentifiers", "CREATE INDEX IF NOT EXISTS cardIdentifiers_uuid ON cardIdentifiers(uuid);"},
                {"cardLegalities", "CREATE INDEX IF NOT EXISTS cardLegalities_uuid ON cardLegalities(uuid);"},
                {"cardPurchaseUrls", "CREATE INDEX IF NOT EXISTS cardPurchaseUrls_uuid ON cardPurchaseUrls(uuid);"},
                {"cardRulings", "CREATE INDEX IF NOT EXISTS cardRulings_uuid ON cardRulings(uuid);"},
                {"cards_uuid", "CREATE INDEX IF NOT EXISTS cards_uuid ON cards(uuid);"},
                {"cards_name", "CREATE INDEX IF NOT EXISTS cards_name ON cards(name);"},
                {"cards_setCode", "CREATE INDEX IF NOT EXISTS cards_setCode ON cards(setCode);"},
                {"cards_side", "CREATE INDEX IF NOT EXISTS cards_side ON cards(side);"},
                {"cards_keywords", "CREATE INDEX IF NOT EXISTS cards_keywords ON cards(keywords);"},
                {"sets_code", "CREATE INDEX IF NOT EXISTS sets_code ON sets(code);"},
                {"sets_tokenSetCode", "CREATE INDEX IF NOT EXISTS sets_tokenSetCode ON sets(tokenSetCode);"},
                {"tokenIdentifiers", "CREATE INDEX IF NOT EXISTS tokenIdentifiers_uuid ON tokenIdentifiers(uuid);"},
                {"tokens_uuid", "CREATE INDEX IF NOT EXISTS tokens_uuid ON tokens(uuid);"},
                {"tokens_name", "CREATE INDEX IF NOT EXISTS tokens_name ON tokens(name);"},
                {"tokens_setCode", "CREATE INDEX IF NOT EXISTS tokens_setCode ON tokens(setCode);"},
                {"tokens_faceName", "CREATE INDEX IF NOT EXISTS tokens_faceName ON tokens(faceName);"},
                {"myCollection", "CREATE INDEX IF NOT EXISTS myCollection_uuid ON myCollection(uuid);"}
            };

            try
            {
                foreach (var item in indices)
                {
                    using var command = new SQLiteCommand(item.Value, DBAccess.connection);
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during creation of indices: {ex.Message}");
                MessageBox.Show($"Error during creation of indices: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        public static async Task CreateViews()
        {
            try
            {
                string createCardTokenViewQuery = @"
                    CREATE VIEW IF NOT EXISTS view_cardToken AS
                    SELECT 
                        c.uuid,
                        c.name,
                        s.name AS setName,
                        c.setCode,
                        NULL AS tokenSetCode,
                        NULL AS faceName
                    FROM 
                        cards c
                    JOIN 
                        sets s ON c.setCode = s.code
                    WHERE 
                        c.side IS NULL OR c.side = 'a'
                    UNION ALL
                    SELECT 
                        t.uuid,
                        t.name,
                        s.name AS setName,
                        s.code AS setCode,
                        s.tokenSetCode,
                        t.faceName
                    FROM 
                        tokens t
                    JOIN 
                        sets s ON t.setCode = s.tokenSetCode
                    WHERE 
                        t.side IS NULL OR t.side = 'a';
                    ";
                string createAllCardsViewQuery = @"
                    CREATE VIEW IF NOT EXISTS view_allCards AS
                    SELECT * FROM (
                        SELECT 
                            c.name AS Name, 
                            s.name AS SetName, 
                            s.releaseDate AS ReleaseDate,
                            k.keyruneImage AS KeyRuneImage, 
                            c.manaCost AS ManaCost, 
                            u.manaCostImage AS ManaCostImage, 
                            c.types AS Types, 
                            c.colors AS Colors,
                            c.supertypes AS SuperTypes, 
                            c.subtypes AS SubTypes, 
                            c.type AS Type, 
                            COALESCE(cg.AggregatedKeywords, c.keywords) AS Keywords,
                            c.text AS RulesText, 
                            c.manaValue AS ManaValue, 
                            c.language AS Language,
                            c.uuid AS Uuid, 
                            c.finishes AS Finishes, 
                            c.side AS Side 
                        FROM cards c
                        JOIN sets s ON c.setCode = s.code
                        LEFT JOIN keyruneImages k ON c.setCode = k.setCode
                        LEFT JOIN uniqueManaCostImages u ON c.manaCost = u.uniqueManaCost
                        LEFT JOIN (
                            SELECT 
                                cc.SetCode, 
                                cc.Name, 
                                GROUP_CONCAT(cc.keywords, ', ') AS AggregatedKeywords
                            FROM cards cc
                            GROUP BY cc.SetCode, cc.Name
                        ) cg ON c.SetCode = cg.SetCode AND c.Name = cg.Name
                        WHERE c.side IS NULL OR c.side = 'a'

                        UNION ALL

                        SELECT 
                            t.name AS Name, 
                            s.name AS SetName, 
                            s.releaseDate AS ReleaseDate,
                            k.keyruneImage AS KeyRuneImage, 
                            t.manaCost AS ManaCost, 
                            u.manaCostImage AS ManaCostImage, 
                            t.types AS Types, 
                            t.colors AS Colors,
                            t.supertypes AS SuperTypes, 
                            t.subtypes AS SubTypes, 
                            t.type AS Type, 
                            t.keywords AS Keywords, 
                            t.text AS RulesText, 
                            NULL AS ManaValue, 
                            t.language AS Language,
                            t.uuid AS Uuid, 
                            t.finishes AS Finishes, 
                            t.side AS Side 
                        FROM tokens t 
                        JOIN sets s ON t.setCode = s.tokenSetCode 
                        LEFT JOIN keyruneImages k ON t.setCode = k.setCode
                        LEFT JOIN uniqueManaCostImages u ON t.manaCost = u.uniqueManaCost
                        WHERE t.side IS NULL OR t.side = 'a'
                    ) ORDER BY ReleaseDate DESC, SetName, Types,
                        CASE Colors
                            WHEN 'W' THEN 1
                            WHEN 'U' THEN 2
                            WHEN 'B' THEN 3
                            WHEN 'R' THEN 4
                            WHEN 'G' THEN 5
                            WHEN 'U' THEN 6
                            ELSE 7
                        END;
                    ";
                string createMyCollectionViewQuery = @"
                    CREATE VIEW IF NOT EXISTS view_myCollection AS
                    SELECT * FROM (
                        SELECT                        
                            c.name AS Name,
                            s.name AS SetName,
                            s.releaseDate AS ReleaseDate,
                            k.keyruneImage AS KeyRuneImage,
                            c.manaCost AS ManaCost,
                            u.manaCostImage AS ManaCostImage,
                            c.types AS Types,
                            c.colors AS Colors,
                            c.supertypes AS SuperTypes,
                            c.subtypes AS SubTypes,
                            c.type AS Type,
                            COALESCE(cg.AggregatedKeywords, c.keywords) AS Keywords,
                            c.text AS RulesText,
                            c.manaValue AS ManaValue,
						    c.finishes AS Finishes,
                            c.uuid AS Uuid,
                            m.id AS CardId,
                            m.count AS CardsOwned,
                            m.trade AS CardsForTrade,
                            m.condition AS Condition,
                            m.language AS Language,
                            m.finish AS Finish,
                            c.side AS Side
                        FROM
                            myCollection m
                        JOIN
                            cards c ON m.uuid = c.uuid
                        LEFT JOIN 
                            sets s ON c.setCode = s.code
                        LEFT JOIN 
                            keyruneImages k ON c.setCode = k.setCode
                        LEFT JOIN 
                            uniqueManaCostImages u ON c.manaCost = u.uniqueManaCost
                        LEFT JOIN (
                            SELECT 
                                cc.SetCode, 
                                cc.Name, 
                                GROUP_CONCAT(cc.keywords, ', ') AS AggregatedKeywords
                            FROM cards cc
                            GROUP BY cc.SetCode, cc.Name
                        ) cg ON c.SetCode = cg.SetCode AND c.Name = cg.Name
                        WHERE EXISTS (SELECT 1 FROM cards WHERE uuid = m.uuid)
                        UNION ALL
                        SELECT
                            t.name AS Name,
                            s.name AS SetName,
                            s.releaseDate AS ReleaseDate,
                            k.keyruneImage AS KeyRuneImage,
                            t.manaCost AS ManaCost,
                            u.manaCostImage AS ManaCostImage,
                            t.types AS Types,
                            t.colors AS Colors,
                            t.supertypes AS SuperTypes,
                            t.subtypes AS SubTypes,
                            t.type AS Type,
                            t.keywords AS Keywords,
                            t.text AS RulesText,
                            NULL AS ManaValue,  -- Tokens do not have manaValue
						    t.finishes AS Finishes,
                            t.uuid AS Uuid,
                            m.id AS CardId,
                            m.count AS CardsOwned,
                            m.trade AS CardsForTrade,
                            m.condition AS Condition,
                            m.language AS Language,
                            m.finish AS Finish,
                            t.side AS Side
                        FROM
                            myCollection m
                        JOIN
                            tokens t ON m.uuid = t.uuid
                        LEFT JOIN 
                            sets s ON t.setCode = s.tokenSetCode
                        LEFT JOIN 
                            keyruneImages k ON t.setCode = k.setCode
                        LEFT JOIN 
                            uniqueManaCostImages u ON t.manaCost = u.uniqueManaCost
                        WHERE NOT EXISTS (SELECT 1 FROM cards WHERE uuid = m.uuid)
                    ) ORDER BY ReleaseDate DESC, SetName, Types,
                        CASE Colors
                            WHEN 'W' THEN 1
                            WHEN 'U' THEN 2
                            WHEN 'B' THEN 3
                            WHEN 'R' THEN 4
                            WHEN 'G' THEN 5
                            ELSE 6
                        END";

                using (var command = new SQLiteCommand(createCardTokenViewQuery, DBAccess.connection))
                {
                    await command.ExecuteNonQueryAsync();
                    Debug.WriteLine("Created view view_cardToken.");
                }

                using (var command = new SQLiteCommand(createAllCardsViewQuery, DBAccess.connection))
                {
                    await command.ExecuteNonQueryAsync();
                    Debug.WriteLine("Created view view_allCards.");
                }

                using (var command = new SQLiteCommand(createMyCollectionViewQuery, DBAccess.connection))
                {
                    await command.ExecuteNonQueryAsync();
                    Debug.WriteLine("Created view view_myCollection.");
                }

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during creation of views: {ex.Message}");
                MessageBox.Show($"Error during creation of views: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}