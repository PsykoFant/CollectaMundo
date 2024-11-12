using Newtonsoft.Json.Linq;
using ServiceStack;
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
        private static readonly System.Net.Http.HttpClient _httpClient = new()
        {
            DefaultRequestHeaders =
            {
                UserAgent = { System.Net.Http.Headers.ProductInfoHeaderValue.Parse("CollectaMundo/1.0") },
                Accept = { System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("application/json") }
            }
        };

        // Download urls 
        public readonly static string cardDbDownloadUrl = "https://mtgjson.com/api/v5/AllPrintings.sqlite";
        public readonly static string pricesDownloadUrl = "https://mtgjson.com/api/v5/AllPricesToday.json";

        // Check if the card database exists in the location specified by appsettings.json. 
        // If it doesn't exist, download it and populate it with custom data, including image data for mana symbols and set images as well as card prices
        public static async Task SystemIntegrityCheckAsync()
        {
            await PrepareDownloadedCardDatabase();

            /*
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
                var downloadDatabaseTask = DownloadResourceFileIfNotExistAsync(databasePath, cardDbDownloadUrl, downloadMessage, "card database", true, true);
                var downloadPricesTask = DownloadResourceFileIfNotExistAsync(CardPriceUtilities.pricesDownloadsPath, pricesDownloadUrl, downloadMessage, "", false);

                // Wait for both tasks to complete using Task.WhenAll
                bool[] results = await Task.WhenAll(downloadPricesTask, downloadDatabaseTask);

                // Check each result and retry if necessary
                if (!results[0])
                {
                    // Redownload card database if the first download failed
                    Debug.WriteLine("Retrying card database download...");
                    bool redownloadCardDb = await DownloadResourceFileIfNotExistAsync(databasePath, cardDbDownloadUrl, downloadMessage, "card database", true, true);
                    if (!redownloadCardDb)
                    {
                        // Handle persistent failure
                        Debug.WriteLine("Card database re-download failed.");
                        await SystemIntegrityCheckAsync();
                        return; // Exit if the re-download fails again
                    }
                }

                if (!results[1])
                {
                    // Redownload prices database if the second download failed
                    Debug.WriteLine("Retrying prices download...");
                    bool redownloadPrices = await DownloadResourceFileIfNotExistAsync(CardPriceUtilities.pricesDownloadsPath, pricesDownloadUrl, downloadMessage, "card prices", false);
                    if (!redownloadPrices)
                    {
                        // Handle persistent failure
                        Debug.WriteLine("Prices re-download failed.");
                        await SystemIntegrityCheckAsync();
                        return; // Exit if the re-download fails again
                    }
                }

                // If both downloads (or re-downloads) succeeded, proceed
                await PrepareDownloadedCardDatabase();
            }
            */
        }
        public static async Task<bool> DownloadResourceFileIfNotExistAsync(string downloadTargetPath, string downloadUrl, string statusMessageBig, string fileToDownloadForMessage, bool showStatusBar, bool forceMessageUpdate = false)
        {
            try
            {
                // Only update the status message if it's either forced or the download message is provided
                if (forceMessageUpdate || !string.IsNullOrEmpty(fileToDownloadForMessage))
                {
                    MainWindow.CurrentInstance.FirstTimeSetupLabel.Content = statusMessageBig;
                }

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

                if (forceMessageUpdate || !string.IsNullOrEmpty(fileToDownloadForMessage))
                {
                    var megabytes = string.Format("{0:0.0} MB", totalBytes / 1000000.0);
                    StatusMessageUpdated?.Invoke($"Downloading {fileToDownloadForMessage} ({megabytes})");
                }

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
        public static async Task PrepareDownloadedCardDatabase()
        {
            await DBAccess.OpenConnectionAsync();

            StatusMessageUpdated?.Invoke("Creating custom tables ...");
            await Task.Run(CreateCustomTables);

            StatusMessageUpdated?.Invoke("Generating mana symbols ...");
            await Task.Run(GenerateManaSymbolsFromSvgAsync);

            StatusMessageUpdated?.Invoke("Generating mana cost images ...");
            await Task.Run(GenerateManaCostImagesAsync);

            StatusMessageUpdated?.Invoke("Generating Set icons ...");
            await Task.Run(GenerateSetKeyruneFromSvgAsync);

            //Stopwatch stopwatch = new();
            //stopwatch.Start();

            StatusMessageUpdated?.Invoke("Updating card prices ...");
            await Task.Run(() => CardPriceUtilities.ImportPricesFromJsonAsync());

            //stopwatch.Stop();
            //Debug.WriteLine($"Import prices tog: {stopwatch.ElapsedMilliseconds} ms");

            StatusMessageUpdated?.Invoke("Finalizing ...");
            var generateIndices = CreateIndices();
            var generateViews = CreateViews();
            await Task.WhenAll(generateIndices, generateViews);

            // Perform database maintenance tasks for optimization
            await DBAccess.OptimizeDb();

            DBAccess.CloseConnection();
        }

        #region Custom Table Operations
        private static async Task CreateCustomTables()
        {
            try
            {
                // Define tables to create
                Dictionary<string, string> tables = new()
                {
                    {"uniqueManaSymbols", "CREATE TABLE IF NOT EXISTS uniqueManaSymbols (uniqueManaSymbol TEXT PRIMARY KEY, manaSymbolImage BLOB);"},
                    {"uniqueManaCostImages", "CREATE TABLE IF NOT EXISTS uniqueManaCostImages (uniqueManaCost TEXT PRIMARY KEY, manaCostImage BLOB);"},
                    {"keyruneImages", "CREATE TABLE IF NOT EXISTS keyruneImages (setCode TEXT PRIMARY KEY, keyruneImage BLOB);"},
                    {"AggregatedCardKeywords", "CREATE TABLE IF NOT EXISTS AggregatedCardKeywords (uuid TEXT PRIMARY KEY, aggregatedKeywords TEXT);"},
                    {"myCollection", "CREATE TABLE IF NOT EXISTS myCollection (id INTEGER PRIMARY KEY AUTOINCREMENT, uuid TEXT, count INTEGER, trade INTEGER, condition TEXT, language TEXT, finish TEXT);"},
                    {"cardPrices", @"CREATE TABLE IF NOT EXISTS cardPrices (uuid TEXT UNIQUE PRIMARY KEY, cardhoarderNormal DECIMAL(10, 2), cardhoarderFoil DECIMAL(10, 2), cardhoarderEtched DECIMAL(10, 2), cardkingdomNormal DECIMAL(10, 2), cardkingdomFoil DECIMAL(10, 2), cardkingdomEtched DECIMAL(10, 2), cardmarketNormal DECIMAL(10, 2), cardmarketFoil DECIMAL(10, 2), cardmarketEtched DECIMAL(10, 2), cardsphereNormal DECIMAL(10, 2), cardsphereFoil DECIMAL(10, 2), cardsphereEtched DECIMAL(10, 2), tcgplayerNormal DECIMAL(10, 2), tcgplayerFoil DECIMAL(10, 2), tcgplayerEtched DECIMAL(10, 2));"}
                };

                // Create the tables asynchronously
                foreach (var item in tables)
                {
                    using var command = new SQLiteCommand(item.Value, DBAccess.connection);
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during creation of tables: {ex.Message}");
                MessageBox.Show($"Error during creation of tables: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Mana symbol and mana cost generation
        private static async Task GenerateManaSymbolsFromSvgAsync()
        {
            try
            {
                List<string> uniqueManaCosts = await DBAccess.GetUniqueValuesAsync("cards", "manaCost");
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
        private static async Task GenerateManaCostImagesAsync()
        {
            try
            {
                List<string> uniqueManaCosts = await DBAccess.GetUniqueValuesAsync("cards", "manaCost");

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
        private static async Task<byte[]> ProcessManaCostInputAsync(string manaCostInput)
        {
            List<Bitmap> manaSymbolImage = [];

            try
            {
                string[] manaSymbols = manaCostInput.Trim(['{', '}']).Split(new string[] { "}{" }, StringSplitOptions.RemoveEmptyEntries);

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
        private static byte[] CombineImages(List<Bitmap> images)
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

        #endregion

        #region Set icon generation
        private static async Task GenerateSetKeyruneFromSvgAsync()
        {
            try
            {
                // Check if the database connection is open
                if (DBAccess.connection == null)
                {
                    throw new InvalidOperationException("Database connection is not initialized.");
                }

                // Step 1: Update missing rows or copy columns in the database
                await CopyColumnIfEmptyOrAddMissingRowsAsync("keyruneImages", "setCode", "sets", "code");
                await CopyColumnIfEmptyOrAddMissingRowsAsync("keyruneImages", "setCode", "sets", "tokenSetCode");

                // Step 2: Get set codes that lack images
                List<string> setCodesWithNoImage = await GetValuesWithNullAsync("keyruneImages", "setCode", "keyruneImage");

                // Step 3: Fetch all set data from the API in one go
                HttpResponseMessage response = await _httpClient.GetAsync("https://api.scryfall.com/sets/");
                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"API Error: {await response.Content.ReadAsStringAsync()}");
                    return;
                }

                string jsonResponse = await response.Content.ReadAsStringAsync();
                JObject allSets = JObject.Parse(jsonResponse);
                JArray? data = allSets["data"] as JArray;

                // Step 4: Process SVGs using parallel tasks efficiently
                var tasks = setCodesWithNoImage.Select(setCode => ProcessSetSvgAsync(setCode, data)).ToList();
                var results = await Task.WhenAll(tasks);

                // Step 5: Perform batch updates to the database inside a single transaction
                using var transaction = DBAccess.connection.BeginTransaction();
                foreach (var (SetCode, PngData) in results.Where(r => r.PngData.Length != 0))
                {
                    await UpdateImageInTableAsync(SetCode, "keyruneImages", "keyruneImage", "setCode", PngData);
                }
                transaction.Commit();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during insertion of keyRuneImages: {ex.Message}");
                MessageBox.Show($"Error during insertion of keyRuneImages: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private static async Task<(string SetCode, byte[] PngData)> ProcessSetSvgAsync(string setCode, JArray? data)
        {
            try
            {
                var matchingSet = data?.FirstOrDefault(x => x["code"]?.ToString().Equals(setCode, StringComparison.OrdinalIgnoreCase) == true);
                string svgUri = matchingSet?["icon_svg_uri"]?.ToString() ?? "https://svgs.scryfall.io/sets/default.svg";
                byte[] pngData = await ConvertSvgToByteArraySharpVectorsAsync(svgUri);
                return (SetCode: setCode, PngData: pngData);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to process SVG for set: {setCode} - {ex.Message}");
                return (SetCode: setCode, PngData: []);
            }
        }
        private static async Task<byte[]> ConvertSvgToByteArraySharpVectorsAsync(string svgUrl)
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
                Debug.WriteLine($"Error converting SVG to byte array for url {svgUrl}: {ex.Message}");
                return [];
            }
        }
        private static async Task CopyColumnIfEmptyOrAddMissingRowsAsync(string targetTable, string targetColumn, string sourceTable, string sourceColumn)
        {
            try
            {
                // Check if there are any missing rows between source and target
                string copyQuery = $@"
                    INSERT INTO {targetTable} ({targetColumn})
                    SELECT DISTINCT {sourceColumn}
                    FROM {sourceTable} 
                    WHERE {sourceColumn} IS NOT NULL 
                      AND {sourceColumn} != '' 
                      AND {sourceColumn} NOT IN (SELECT DISTINCT {targetColumn} FROM {targetTable} WHERE {targetColumn} IS NOT NULL AND {targetColumn} != '');";

                // Execute the query to copy missing rows
                using var copyCommand = new SQLiteCommand(copyQuery, DBAccess.connection);
                int rowsCopied = await copyCommand.ExecuteNonQueryAsync();

                Debug.WriteLine($"Copied {rowsCopied} missing rows from {sourceTable}.{sourceColumn} to {targetTable}.{targetColumn}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("An error occurred while copying missing rows: " + ex.Message);
                MessageBox.Show($"An error occurred while copying missing rows: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Indices and views generation
        private static async Task CreateIndices()
        {
            Dictionary<string, string> indices = new()
{
                {"idx_uniquemanasymbols_uniquemanasymbol", "CREATE INDEX IF NOT EXISTS idx_uniquemanasymbols_uniquemanasymbol ON uniqueManaSymbols(uniqueManaSymbol);"},
                {"idx_uniquemanaCostimages_uniquemanaCost", "CREATE INDEX IF NOT EXISTS idx_uniquemanaCostimages_uniquemanaCost ON uniqueManaCostImages(uniqueManaCost);"},
                {"idx_keyruneimages_setcode", "CREATE INDEX IF NOT EXISTS idx_keyruneimages_setcode ON keyruneImages(setCode);"},
                {"idx_cardprices_uuid", "CREATE INDEX IF NOT EXISTS idx_cardprices_uuid ON cardPrices(uuid);"},
                {"idx_cardidentifiers_uuid", "CREATE INDEX IF NOT EXISTS idx_cardidentifiers_uuid ON cardIdentifiers(uuid);"},
                {"idx_cardforeigndata_uuid", "CREATE INDEX IF NOT EXISTS idx_cardforeigndata_uuid ON cardForeignData(uuid);"},
                {"idx_cardlegalities_uuid", "CREATE INDEX IF NOT EXISTS idx_cardlegalities_uuid ON cardLegalities(uuid);"},
                {"idx_cards_uuid", "CREATE INDEX IF NOT EXISTS idx_cards_uuid ON cards(uuid);"},
                {"idx_cards_setcode_name", "CREATE INDEX IF NOT EXISTS idx_cards_setcode_name ON cards(setCode, name);"},
                {"idx_tokens_setcode_name", "CREATE INDEX IF NOT EXISTS idx_tokens_setcode_name ON tokens(setCode, name);"},
                {"idx_cards_keywords", "CREATE INDEX IF NOT EXISTS idx_cards_keywords ON cards(keywords);"},
                {"idx_sets_tokenSetcode", "CREATE INDEX IF NOT EXISTS idx_sets_tokenSetcode ON sets(tokenSetCode);"},
                {"idx_tokenidentifiers_uuid", "CREATE INDEX IF NOT EXISTS idx_tokenidentifiers_uuid ON tokenIdentifiers(uuid);"},
                {"idx_tokens_uuid", "CREATE INDEX IF NOT EXISTS idx_tokens_uuid ON tokens(uuid);"},
                {"idx_tokens_name", "CREATE INDEX IF NOT EXISTS idx_tokens_name ON tokens(name);"},
                {"idx_tokens_facename", "CREATE INDEX IF NOT EXISTS idx_tokens_facename ON tokens(faceName);"},
                {"idx_mycollection_uuid", "CREATE INDEX IF NOT EXISTS idx_mycollection_uuid ON myCollection(uuid);"},
                {"idx_cards_side_uuid", "CREATE INDEX IF NOT EXISTS idx_cards_side_uuid ON cards(side, uuid);"},
                {"idx_tokens_side_uuid", "CREATE INDEX IF NOT EXISTS idx_tokens_side_uuid ON tokens(side, uuid);"},
                {"idx_sets_code_tokensetcode", "CREATE INDEX IF NOT EXISTS idx_sets_code_tokensetcode ON sets(code, tokenSetCode);"},
                {"idx_cards_setcode_name_type", "CREATE INDEX IF NOT EXISTS idx_cards_setcode_name_type ON cards(setCode, name, type);"},
                {"idx_tokens_setcode_name_type", "CREATE INDEX IF NOT EXISTS idx_tokens_setcode_name_type ON tokens(setCode, name, type);"}
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
                // Build the retailer-specific price column names based on the retailer setting
                string normalPriceColumn = $"p.{MainWindow.CurrentInstance.appsettingsRetailer}Normal AS NormalPrice";
                string foilPriceColumn = $"p.{MainWindow.CurrentInstance.appsettingsRetailer}Foil AS FoilPrice";
                string etchedPriceColumn = $"p.{MainWindow.CurrentInstance.appsettingsRetailer}Etched AS EtchedPrice";

                // Drop existing views if they exist
                string dropAllCardsViewQuery = "DROP VIEW IF EXISTS view_allCards;";
                string dropMyCollectionViewQuery = "DROP VIEW IF EXISTS view_myCollection;";

                // Create the views
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
                string createAllCardsViewQuery = $@"
                    CREATE VIEW view_allCards AS
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
                            c.side AS Side,
                            {normalPriceColumn},
                            {foilPriceColumn},
                            {etchedPriceColumn}
                        FROM cards c
                        JOIN sets s ON c.setCode = s.code
                        LEFT JOIN keyruneImages k ON c.setCode = k.setCode
                        LEFT JOIN uniqueManaCostImages u ON c.manaCost = u.uniqueManaCost
                        LEFT JOIN cardPrices p ON c.uuid = p.uuid
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
                            t.side AS Side,
                            {normalPriceColumn},
                            {foilPriceColumn},
                            {etchedPriceColumn}
                        FROM tokens t 
                        JOIN sets s ON t.setCode = s.tokenSetCode 
                        LEFT JOIN keyruneImages k ON t.setCode = k.setCode
                        LEFT JOIN uniqueManaCostImages u ON t.manaCost = u.uniqueManaCost
                        LEFT JOIN cardPrices p ON t.uuid = p.uuid
                        WHERE t.side IS NULL OR t.side = 'a'
                    ) 
                    ORDER BY ReleaseDate DESC, SetName, Types,
                        CASE Colors
                            WHEN 'W' THEN 1
                            WHEN 'U' THEN 2
                            WHEN 'B' THEN 3
                            WHEN 'R' THEN 4
                            WHEN 'G' THEN 5
                            ELSE 7
                        END;
                    ";
                string createMyCollectionViewQuery = $@"
                    CREATE VIEW view_myCollection AS
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
                            c.side AS Side,
                            {normalPriceColumn},
                            {foilPriceColumn},
                            {etchedPriceColumn}
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
                        LEFT JOIN 
                            cardPrices p ON m.uuid = p.uuid	
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
                            NULL AS ManaValue,
                            t.finishes AS Finishes,
                            t.uuid AS Uuid,
                            m.id AS CardId,
                            m.count AS CardsOwned,
                            m.trade AS CardsForTrade,
                            m.condition AS Condition,
                            m.language AS Language,
                            m.finish AS Finish,
                            t.side AS Side,
                            {normalPriceColumn},
                            {foilPriceColumn},
                            {etchedPriceColumn}
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
                        LEFT JOIN 
                            cardPrices p ON m.uuid = p.uuid
                        WHERE NOT EXISTS (SELECT 1 FROM cards WHERE uuid = m.uuid)
                    ) ORDER BY ReleaseDate DESC, SetName, Types,
                        CASE Colors
                            WHEN 'W' THEN 1
                            WHEN 'U' THEN 2
                            WHEN 'B' THEN 3
                            WHEN 'R' THEN 4
                            WHEN 'G' THEN 5
                            ELSE 6
                        END;
                    ";

                using (var command = new SQLiteCommand(createCardTokenViewQuery, DBAccess.connection))
                {
                    await command.ExecuteNonQueryAsync();
                }

                using (var command = new SQLiteCommand(dropAllCardsViewQuery, DBAccess.connection))
                {
                    await command.ExecuteNonQueryAsync();
                }
                using (var command = new SQLiteCommand(createAllCardsViewQuery, DBAccess.connection))
                {
                    await command.ExecuteNonQueryAsync();
                }

                using (var command = new SQLiteCommand(dropMyCollectionViewQuery, DBAccess.connection))
                {
                    await command.ExecuteNonQueryAsync();
                }
                using (var command = new SQLiteCommand(createMyCollectionViewQuery, DBAccess.connection))
                {
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during creation of views: {ex.Message}");
                MessageBox.Show($"Error during creation of views: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Shared helper methods
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
                    valuesWithNull.Add(reader[returnColumnName]?.ToString() ?? string.Empty);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error retrieving values with null: {ex.Message}");
                MessageBox.Show($"Error retrieving values with null: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return valuesWithNull;
        }


        #endregion
    }
}