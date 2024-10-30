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
        private readonly static string pricesDownloadUrl = "https://downloads.s3.cardmarket.com/productCatalog/priceGuide/price_guide_1.json";

        // Check if the card database exists in the location specified by appsettings.json. 
        // If it doesn't exist, download it and populate it with custom data, including image data for mana symbols and set images as well as card prices
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
                var downloadDatabaseTask = DownloadResourceFileIfNotExistAsync(databasePath, cardDbDownloadUrl, downloadMessage, "card database", true);
                var downloadPricesTask = DownloadResourceFileIfNotExistAsync(MainWindow.priceDownloadsPath, pricesDownloadUrl, downloadMessage, "", false);

                // Wait for both tasks to complete using Task.WhenAll
                bool[] results = await Task.WhenAll(downloadPricesTask, downloadDatabaseTask);

                // Check each result and retry if necessary
                if (!results[0])
                {
                    // Redownload card database if the first download failed
                    Debug.WriteLine("Retrying card database download...");
                    bool redownloadCardDb = await DownloadResourceFileIfNotExistAsync(databasePath, cardDbDownloadUrl, downloadMessage, "card database", true);
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
                    bool redownloadPrices = await DownloadResourceFileIfNotExistAsync(MainWindow.priceDownloadsPath, pricesDownloadUrl, downloadMessage, "", false);
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
        }
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
        public static async Task PrepareDownloadedCardDatabase()
        {
            await DBAccess.OpenConnectionAsync();

            StatusMessageUpdated?.Invoke("Generating mana symbols ...");
            await CreateCustomTables();
            await GenerateManaSymbolsFromSvgAsync();

            StatusMessageUpdated?.Invoke("Generating mana cost images ...");
            await Task.Run(GenerateManaCostImagesAsync);

            StatusMessageUpdated?.Invoke("Generating Set icons ...");
            await Task.Run(GenerateSetKeyruneFromSvgAsync);

            StatusMessageUpdated?.Invoke("Updating card prices ...");
            await Task.Run(() => ImportPricesFromJsonAsync(32000));

            StatusMessageUpdated?.Invoke("Finalizing ...");
            var generateIndices = CreateIndices();
            var generateViews = CreateViews();
            await Task.WhenAll(generateIndices, generateViews);

            DBAccess.CloseConnection();
        }
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
                    {"cardPrices", @"CREATE TABLE IF NOT EXISTS cardPrices (uuid TEXT UNIQUE PRIMARY KEY, mcmId INTEGER, avg DECIMAL(10, 2), low DECIMAL(10, 2), trend DECIMAL(10, 2), avg1 DECIMAL(10, 2), avg7 DECIMAL(10, 2), avg30 DECIMAL(10, 2), avgFoil DECIMAL(10, 2), lowFoil DECIMAL(10, 2), trendFoil DECIMAL(10, 2), avg1Foil DECIMAL(10, 2), avg7Foil DECIMAL(10, 2), avg30Foil DECIMAL(10, 2));"}
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
        public static async Task GenerateManaSymbolsFromSvgAsync()
        {
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
        public static async Task ImportPricesFromJsonAsync(int batchSize)
        {
            await CopyNonNullMcmIdsToCardPricesAsync();

            try
            {
                // Read the JSON file from the priceDownloadsPath
                string jsonFilePath = MainWindow.priceDownloadsPath;
                if (!File.Exists(jsonFilePath))
                {
                    throw new FileNotFoundException($"Price JSON file not found at: {jsonFilePath}");
                }

                // Check if the database connection is open
                if (DBAccess.connection == null)
                {
                    throw new InvalidOperationException("Database connection is not initialized.");
                }

                // Read the JSON content
                var jsonContent = await File.ReadAllTextAsync(jsonFilePath);

                // Parse the createdAt field separately from priceGuides
                var jsonObject = JObject.Parse(jsonContent);
                string createdAt = jsonObject["createdAt"]?.ToString() ?? throw new InvalidOperationException("CreatedAt not found in JSON.");

                // Deserialize priceGuides directly into a list of PriceGuide objects
                var priceGuides = jsonObject["priceGuides"]?.ToObject<List<PriceGuide>>() ?? new List<PriceGuide>();
                if (priceGuides.Count == 0)
                {
                    throw new InvalidOperationException("No price data found in the JSON file.");
                }

                // Step 1: Load McmId-UUID mapping into a dictionary (Object 1)
                var cardPricesMap = new Dictionary<int, (string uuid, PriceGuide prices)>();
                string loadUuidMapSql = "SELECT mcmId, uuid FROM cardPrices WHERE mcmId IS NOT NULL;";
                using (var command = new SQLiteCommand(loadUuidMapSql, DBAccess.connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        int mcmId = reader.GetInt32(0);
                        string uuid = reader.GetString(1);
                        cardPricesMap[mcmId] = (uuid, new PriceGuide());
                    }
                }

                // Step 2: Update Object 1 with prices from JSON (Object 2)
                foreach (var priceGuide in priceGuides)
                {
                    if (cardPricesMap.TryGetValue(priceGuide.IdProduct, out var existingData))
                    {
                        // Update prices within Object 1
                        cardPricesMap[priceGuide.IdProduct] = (existingData.uuid, priceGuide);
                    }
                }

                // Step 3: Insert updated prices into the 'cardPrices' table within a transaction
                string insertSql = @"INSERT OR REPLACE INTO cardPrices (uuid, mcmId, Avg, Low, Trend, Avg1, Avg7, Avg30, AvgFoil, LowFoil, TrendFoil, Avg1Foil, Avg7Foil, Avg30Foil)
                            VALUES (@uuid, @mcmId, @Avg, @Low, @Trend, @Avg1, @Avg7, @Avg30, @AvgFoil, @LowFoil, @TrendFoil, @Avg1Foil, @Avg7Foil, @Avg30Foil);";

                var transaction = DBAccess.connection.BeginTransaction();
                try
                {
                    using var insertCommand = new SQLiteCommand(insertSql, DBAccess.connection);
                    insertCommand.Transaction = transaction;

                    // Prepare reusable command parameters
                    insertCommand.Parameters.Add(new SQLiteParameter("@uuid"));
                    insertCommand.Parameters.Add(new SQLiteParameter("@mcmId"));
                    insertCommand.Parameters.Add(new SQLiteParameter("@Avg"));
                    insertCommand.Parameters.Add(new SQLiteParameter("@Low"));
                    insertCommand.Parameters.Add(new SQLiteParameter("@Trend"));
                    insertCommand.Parameters.Add(new SQLiteParameter("@Avg1"));
                    insertCommand.Parameters.Add(new SQLiteParameter("@Avg7"));
                    insertCommand.Parameters.Add(new SQLiteParameter("@Avg30"));
                    insertCommand.Parameters.Add(new SQLiteParameter("@AvgFoil"));
                    insertCommand.Parameters.Add(new SQLiteParameter("@LowFoil"));
                    insertCommand.Parameters.Add(new SQLiteParameter("@TrendFoil"));
                    insertCommand.Parameters.Add(new SQLiteParameter("@Avg1Foil"));
                    insertCommand.Parameters.Add(new SQLiteParameter("@Avg7Foil"));
                    insertCommand.Parameters.Add(new SQLiteParameter("@Avg30Foil"));

                    int counter = 0;

                    foreach (var (mcmId, (uuid, prices)) in cardPricesMap)
                    {
                        insertCommand.Parameters["@uuid"].Value = uuid;
                        insertCommand.Parameters["@mcmId"].Value = mcmId;
                        insertCommand.Parameters["@Avg"].Value = prices.Avg ?? (object)DBNull.Value;
                        insertCommand.Parameters["@Low"].Value = prices.Low ?? (object)DBNull.Value;
                        insertCommand.Parameters["@Trend"].Value = prices.Trend ?? (object)DBNull.Value;
                        insertCommand.Parameters["@Avg1"].Value = prices.Avg1 ?? (object)DBNull.Value;
                        insertCommand.Parameters["@Avg7"].Value = prices.Avg7 ?? (object)DBNull.Value;
                        insertCommand.Parameters["@Avg30"].Value = prices.Avg30 ?? (object)DBNull.Value;
                        insertCommand.Parameters["@AvgFoil"].Value = prices.AvgFoil ?? (object)DBNull.Value;
                        insertCommand.Parameters["@LowFoil"].Value = prices.LowFoil ?? (object)DBNull.Value;
                        insertCommand.Parameters["@TrendFoil"].Value = prices.TrendFoil ?? (object)DBNull.Value;
                        insertCommand.Parameters["@Avg1Foil"].Value = prices.Avg1Foil ?? (object)DBNull.Value;
                        insertCommand.Parameters["@Avg7Foil"].Value = prices.Avg7Foil ?? (object)DBNull.Value;
                        insertCommand.Parameters["@Avg30Foil"].Value = prices.Avg30Foil ?? (object)DBNull.Value;

                        await insertCommand.ExecuteNonQueryAsync();

                        if (++counter % batchSize == 0)
                        {
                            await transaction.CommitAsync();
                            transaction.Dispose();
                            transaction = DBAccess.connection.BeginTransaction();
                        }
                    }

                    await transaction.CommitAsync();
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
                finally
                {
                    transaction.Dispose();
                }

                // Update the PricesUpdatedDate in appsettings.json
                ConfigurationManager.UpdatePriceInfo(createdAt, null);

                File.Delete(jsonFilePath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during price import: {ex.Message}");
                MessageBox.Show($"Error during price import: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        public static async Task CreateIndices()
        {
            Dictionary<string, string> indices = new()
{
                {"idx_uniquemanasymbols_uniquemanasymbol", "CREATE INDEX IF NOT EXISTS idx_uniquemanasymbols_uniquemanasymbol ON uniqueManaSymbols(uniqueManaSymbol);"},
                {"idx_uniquemanaCostimages_uniquemanaCost", "CREATE INDEX IF NOT EXISTS idx_uniquemanaCostimages_uniquemanaCost ON uniqueManaCostImages(uniqueManaCost);"},
                {"idx_keyruneimages_setcode", "CREATE INDEX IF NOT EXISTS idx_keyruneimages_setcode ON keyruneImages(setCode);"},
                {"idx_cardprices_mcmid", "CREATE INDEX IF NOT EXISTS idx_cardprices_mcmid ON cardPrices(mcmId);"},
                {"idx_cardprices_uuid", "CREATE INDEX IF NOT EXISTS idx_cardprices_uuid ON cardPrices(uuid);"},
                {"idx_cardidentifiers_uuid", "CREATE INDEX IF NOT EXISTS idx_cardidentifiers_uuid ON cardIdentifiers(uuid);"},
                {"idx_cardidentifiers_mcmid", "CREATE INDEX IF NOT EXISTS idx_cardidentifiers_mcmid ON cardIdentifiers(mcmId);"},
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
                {"idx_tokens_setcode_name_type", "CREATE INDEX IF NOT EXISTS idx_tokens_setcode_name_type ON tokens(setCode, name, type);"},
                {"idx_cards_setcode_name", "CREATE INDEX IF NOT EXISTS idx_cards_setcode_name ON cards(setCode, name);"}
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
                            p.avg AS AvgPrice,
                            p.avgFoil AS AvgFoilPrice,
							p.low AS LowPrice,
							p.lowFoil AS LowFoilPrice,
							p.trend AS TrendPrice,
							p.trendFoil AS TrendFoilPrice,
							p.avg1 AS Avg1Price,
							p.avg1Foil AS Avg1FoilPrice,
							p.avg7 AS Avg7Price,
							p.avg7Foil AS Avg7FoilPrice,
							p.avg30 AS Avg30Price,
							p.avg30Foil AS Avg30FoilPrice
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
                            p.avg AS AvgPrice,
                            p.avgFoil AS AvgFoilPrice,
							p.low AS LowPrice,
							p.lowFoil AS LowFoilPrice,
							p.trend AS TrendPrice,
							p.trendFoil AS TrendFoilPrice,
							p.avg1 AS Avg1Price,
							p.avg1Foil AS Avg1FoilPrice,
							p.avg7 AS Avg7Price,
							p.avg7Foil AS Avg7FoilPrice,
							p.avg30 AS Avg30Price,
							p.avg30Foil AS Avg30FoilPrice
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
                            c.side AS Side,
		                    p.avg AS AvgPrice,
		                    p.avgFoil AS AvgFoilPrice,
		                    p.low AS LowPrice,
		                    p.lowFoil AS LowFoilPrice,
		                    p.trend AS TrendPrice,
		                    p.trendFoil AS TrendFoilPrice,
		                    p.avg1 AS Avg1Price,
		                    p.avg1Foil AS Avg1FoilPrice,
		                    p.avg7 AS Avg7Price,
		                    p.avg7Foil AS Avg7FoilPrice,
		                    p.avg30 AS Avg30Price,
		                    p.avg30Foil AS Avg30FoilPrice
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
                            NULL AS ManaValue,  -- Tokens do not have manaValue
				                        t.finishes AS Finishes,
                            t.uuid AS Uuid,
                            m.id AS CardId,
                            m.count AS CardsOwned,
                            m.trade AS CardsForTrade,
                            m.condition AS Condition,
                            m.language AS Language,
                            m.finish AS Finish,
                            t.side AS Side,
		                    p.avg AS AvgPrice,
		                    p.avgFoil AS AvgFoilPrice,
		                    p.low AS LowPrice,
		                    p.lowFoil AS LowFoilPrice,
		                    p.trend AS TrendPrice,
		                    p.trendFoil AS TrendFoilPrice,
		                    p.avg1 AS Avg1Price,
		                    p.avg1Foil AS Avg1FoilPrice,
		                    p.avg7 AS Avg7Price,
		                    p.avg7Foil AS Avg7FoilPrice,
		                    p.avg30 AS Avg30Price,
		                    p.avg30Foil AS Avg30FoilPrice
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

                using (var command = new SQLiteCommand(createAllCardsViewQuery, DBAccess.connection))
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
        private static async Task CopyNonNullMcmIdsToCardPricesAsync()
        {
            try
            {
                // Step 1: Define the SQL statement for copying data
                string copySql = @"
                    INSERT OR IGNORE INTO cardPrices (uuid, mcmId)
                    SELECT ci.uuid, ci.mcmId
                    FROM cardIdentifiers ci
                    JOIN cards c ON ci.uuid = c.uuid
                    WHERE ci.mcmId IS NOT NULL AND (c.side IS NULL OR c.side = 'a')

                    UNION

                    SELECT ti.uuid, ti.mcmId
                    FROM tokenIdentifiers ti
                    JOIN tokens t ON ti.uuid = t.uuid
                    WHERE ti.mcmId IS NOT NULL AND (t.side IS NULL OR t.side = 'a');";

                // Step 2: Execute the SQL statement
                using var command = new SQLiteCommand(copySql, DBAccess.connection);
                int rowsCopied = await command.ExecuteNonQueryAsync();

                Debug.WriteLine($"{rowsCopied} rows copied from cardIdentifiers and tokenIdentifiers to cardPrices.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error copying data from cardIdentifiers and tokenIdentifiers to cardPrices: {ex.Message}");
                MessageBox.Show($"Error copying data from cardIdentifiers and tokenIdentifiers to cardPrices: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}