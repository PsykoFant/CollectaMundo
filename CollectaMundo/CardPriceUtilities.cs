using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace CollectaMundo
{
    public class CardPriceUtilities
    {
        public static event Action<string>? StatusMessageUpdated;
        // Temporary price download location 
        public readonly static string pricesDownloadsPath = Path.Combine(MainWindow.currentUserFolders, "Downloads", "prices.json");
        public static async Task UpdatePricesAsync()
        {
            try
            {
                MainWindow.CurrentInstance.GridTopMenu.IsEnabled = false;
                MainWindow.CurrentInstance.GridSideMenu.IsEnabled = false;

                string? dateString = ConfigurationManager.GetSetting("PriceInfo:PricesUpdatedDate") as string;

                if (DateTime.TryParse(dateString, out DateTime priceInfoDate))
                {
                    if (priceInfoDate < DateTime.Today)
                    {
                        Debug.WriteLine($"The date in appsettings ({priceInfoDate}) is older than today ({DateTime.Today})");
                        await MainWindow.ShowStatusWindowAsync(true);

                        if (await DownloadAndPrepDB.DownloadResourceFileIfNotExistAsync(pricesDownloadsPath, DownloadAndPrepDB.pricesDownloadUrl, "Updating card prices - please wait...", "Downloading price file...", true))
                        {
                            StatusMessageUpdated?.Invoke("Updating card prices ...");
                            await DBAccess.OpenConnectionAsync();

                            await ImportPricesFromJsonAsync(32000);

                            MainWindow.CurrentInstance.UtilsInfoLabel.Content = "Card prices have been updated ...";
                        }
                        else
                        {
                            MainWindow.CurrentInstance.UtilsInfoLabel.Content = "Something went wrong downloading new prices :-( ...";
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"Date in appsettings: {priceInfoDate}");

                        MainWindow.CurrentInstance.UtilsInfoLabel.Content = "Your card prices are already up to date.";
                    }
                }
                else
                {
                    Debug.WriteLine("Failed to parse the date from appsettings.");
                    throw new FormatException("Invalid date format in appsettings.json for 'PriceInfo:PricesUpdatedDate'.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"An error occurred updating card prices: {ex.Message}");
                MessageBox.Show($"An error occurred updating card prices: {ex.Message}", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                DBAccess.CloseConnection();
                MainWindow.CurrentInstance.GridTopMenu.IsEnabled = true;
                MainWindow.CurrentInstance.GridSideMenu.IsEnabled = true;
                await MainWindow.ShowStatusWindowAsync(false);
            }

        }
        public static async Task ImportPricesFromJsonAsync(int batchSize)
        {
            try
            {
                // Read the JSON file from the pricesDownloadsPath
                string jsonFilePath = pricesDownloadsPath;
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
    }
    public class PriceGuide
    {
        public int IdProduct { get; set; }
        public decimal? Avg { get; set; }
        public decimal? Low { get; set; }
        public decimal? Trend { get; set; }
        public decimal? Avg1 { get; set; }
        public decimal? Avg7 { get; set; }
        public decimal? Avg30 { get; set; }

        [JsonProperty("avg-foil")]
        public decimal? AvgFoil { get; set; }

        [JsonProperty("low-foil")]
        public decimal? LowFoil { get; set; }

        [JsonProperty("trend-foil")]
        public decimal? TrendFoil { get; set; }

        [JsonProperty("avg1-foil")]
        public decimal? Avg1Foil { get; set; }

        [JsonProperty("avg7-foil")]
        public decimal? Avg7Foil { get; set; }

        [JsonProperty("avg30-foil")]
        public decimal? Avg30Foil { get; set; }
    }

}


