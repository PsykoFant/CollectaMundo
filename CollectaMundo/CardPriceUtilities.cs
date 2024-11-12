using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

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
                        await MainWindow.ShowStatusWindowAsync(true, null, true);

                        if (await DownloadAndPrepDB.DownloadResourceFileIfNotExistAsync(pricesDownloadsPath, DownloadAndPrepDB.pricesDownloadUrl, "Updating card prices - please wait...", "price file...", true, true))
                        {
                            StatusMessageUpdated?.Invoke("Updating card prices ...");
                            await DBAccess.OpenConnectionAsync();

                            await Task.Run(() => ImportPricesFromJsonAsync(20000));

                            StatusMessageUpdated?.Invoke("Processing new prices and reloading card database ...");

                            // Reload cards to get updated prices
                            Task loadAllCards = MainWindow.PopulateCardDataGridAsync(MainWindow.CurrentInstance.allCards, MainWindow.CurrentInstance.allCardsQuery, MainWindow.CurrentInstance.AllCardsDataGrid, false);
                            Task loadMyCollection = MainWindow.PopulateCardDataGridAsync(MainWindow.CurrentInstance.myCards, MainWindow.CurrentInstance.myCollectionQuery, MainWindow.CurrentInstance.MyCollectionDataGrid, true);
                            await Task.WhenAll(loadAllCards, loadMyCollection);

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
            var totalWatch = Stopwatch.StartNew();
            var stopwatch = new Stopwatch();

            try
            {
                // Measure the time to read the JSON file
                stopwatch.Start();
                string jsonFilePath = pricesDownloadsPath;
                if (!File.Exists(jsonFilePath))
                {
                    throw new FileNotFoundException($"Price JSON file not found at: {jsonFilePath}");
                }

                var jsonContent = await File.ReadAllTextAsync(jsonFilePath);
                stopwatch.Stop();
                Debug.WriteLine($"Time to read JSON file: {stopwatch.ElapsedMilliseconds} ms");

                // Measure the time to parse JSON content with JsonDocument
                stopwatch.Restart();
                using var jsonDocument = JsonDocument.Parse(jsonContent);
                var root = jsonDocument.RootElement;

                // Extract the createdAt date
                string createdAt = root.GetProperty("meta").GetProperty("date").GetString()
                                  ?? throw new InvalidOperationException("Meta:date not found in JSON.");

                var priceData = root.GetProperty("data");
                stopwatch.Stop();
                Debug.WriteLine($"Time to parse JSON content: {stopwatch.ElapsedMilliseconds} ms");

                // Prepare SQL statement and start transaction for batch insertion
                stopwatch.Restart();
                string insertSql = @"
            INSERT OR REPLACE INTO cardPrices (uuid, cardhoarderNormal, cardhoarderFoil, cardhoarderEtched, 
                cardkingdomNormal, cardkingdomFoil, cardkingdomEtched, cardmarketNormal, cardmarketFoil, cardmarketEtched, 
                cardsphereNormal, cardsphereFoil, cardsphereEtched, tcgplayerNormal, tcgplayerFoil, tcgplayerEtched)
            VALUES (@uuid, @cardhoarderNormal, @cardhoarderFoil, @cardhoarderEtched, 
                @cardkingdomNormal, @cardkingdomFoil, @cardkingdomEtched, @cardmarketNormal, @cardmarketFoil, @cardmarketEtched, 
                @cardsphereNormal, @cardsphereFoil, @cardsphereEtched, @tcgplayerNormal, @tcgplayerFoil, @tcgplayerEtched);";

                var transaction = DBAccess.connection.BeginTransaction();
                stopwatch.Stop();
                Debug.WriteLine($"Time to start transaction and prepare SQL: {stopwatch.ElapsedMilliseconds} ms");

                int counter = 0;

                try
                {
                    // Begin timing for the database insertions
                    stopwatch.Restart();
                    using var insertCommand = new SQLiteCommand(insertSql, DBAccess.connection);
                    insertCommand.Transaction = transaction;

                    // Prepare reusable command parameters
                    insertCommand.Parameters.Add(new SQLiteParameter("@uuid"));
                    insertCommand.Parameters.Add(new SQLiteParameter("@cardhoarderNormal"));
                    insertCommand.Parameters.Add(new SQLiteParameter("@cardhoarderFoil"));
                    insertCommand.Parameters.Add(new SQLiteParameter("@cardhoarderEtched"));
                    insertCommand.Parameters.Add(new SQLiteParameter("@cardkingdomNormal"));
                    insertCommand.Parameters.Add(new SQLiteParameter("@cardkingdomFoil"));
                    insertCommand.Parameters.Add(new SQLiteParameter("@cardkingdomEtched"));
                    insertCommand.Parameters.Add(new SQLiteParameter("@cardmarketNormal"));
                    insertCommand.Parameters.Add(new SQLiteParameter("@cardmarketFoil"));
                    insertCommand.Parameters.Add(new SQLiteParameter("@cardmarketEtched"));
                    insertCommand.Parameters.Add(new SQLiteParameter("@cardsphereNormal"));
                    insertCommand.Parameters.Add(new SQLiteParameter("@cardsphereFoil"));
                    insertCommand.Parameters.Add(new SQLiteParameter("@cardsphereEtched"));
                    insertCommand.Parameters.Add(new SQLiteParameter("@tcgplayerNormal"));
                    insertCommand.Parameters.Add(new SQLiteParameter("@tcgplayerFoil"));
                    insertCommand.Parameters.Add(new SQLiteParameter("@tcgplayerEtched"));

                    // Iterate through the price data in the JSON using JsonDocument
                    foreach (var priceToken in priceData.EnumerateObject())
                    {
                        string uuid = priceToken.Name;
                        var priceList = ParsePriceList(priceToken.Value);

                        if (priceList != null)
                        {
                            insertCommand.Parameters["@uuid"].Value = uuid;
                            insertCommand.Parameters["@cardhoarderNormal"].Value = priceList.CardhoarderNormal ?? (object)DBNull.Value;
                            insertCommand.Parameters["@cardhoarderFoil"].Value = priceList.CardhoarderFoil ?? (object)DBNull.Value;
                            insertCommand.Parameters["@cardhoarderEtched"].Value = priceList.CardhoarderEtched ?? (object)DBNull.Value;
                            insertCommand.Parameters["@cardkingdomNormal"].Value = priceList.CardkingdomNormal ?? (object)DBNull.Value;
                            insertCommand.Parameters["@cardkingdomFoil"].Value = priceList.CardkingdomFoil ?? (object)DBNull.Value;
                            insertCommand.Parameters["@cardkingdomEtched"].Value = priceList.CardkingdomEtched ?? (object)DBNull.Value;
                            insertCommand.Parameters["@cardmarketNormal"].Value = priceList.CardmarketNormal ?? (object)DBNull.Value;
                            insertCommand.Parameters["@cardmarketFoil"].Value = priceList.CardmarketFoil ?? (object)DBNull.Value;
                            insertCommand.Parameters["@cardmarketEtched"].Value = priceList.CardmarketEtched ?? (object)DBNull.Value;
                            insertCommand.Parameters["@cardsphereNormal"].Value = priceList.CardsphereNormal ?? (object)DBNull.Value;
                            insertCommand.Parameters["@cardsphereFoil"].Value = priceList.CardsphereFoil ?? (object)DBNull.Value;
                            insertCommand.Parameters["@cardsphereEtched"].Value = priceList.CardsphereEtched ?? (object)DBNull.Value;
                            insertCommand.Parameters["@tcgplayerNormal"].Value = priceList.TcgplayerNormal ?? (object)DBNull.Value;
                            insertCommand.Parameters["@tcgplayerFoil"].Value = priceList.TcgplayerFoil ?? (object)DBNull.Value;
                            insertCommand.Parameters["@tcgplayerEtched"].Value = priceList.TcgplayerEtched ?? (object)DBNull.Value;

                            await insertCommand.ExecuteNonQueryAsync();

                            // Measure batch commit times
                            if (++counter % batchSize == 0)
                            {
                                stopwatch.Stop();
                                Debug.WriteLine($"Batch insertion time for {batchSize} records: {stopwatch.ElapsedMilliseconds} ms");

                                stopwatch.Restart();
                                await transaction.CommitAsync();
                                transaction.Dispose();
                                transaction = DBAccess.connection.BeginTransaction();
                                stopwatch.Stop();
                                Debug.WriteLine($"Time to commit transaction: {stopwatch.ElapsedMilliseconds} ms");

                                stopwatch.Restart();
                            }
                        }
                    }

                    stopwatch.Stop();
                    Debug.WriteLine($"Total insertion time: {stopwatch.ElapsedMilliseconds} ms");

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

                // Time to update settings
                stopwatch.Restart();
                ConfigurationManager.UpdatePriceInfo(createdAt, null);
                stopwatch.Stop();
                Debug.WriteLine($"Time to update settings: {stopwatch.ElapsedMilliseconds} ms");

                totalWatch.Stop();
                Debug.WriteLine($"Total time for ImportPricesFromJsonAsync: {totalWatch.ElapsedMilliseconds} ms");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during price import: {ex.Message}");
                MessageBox.Show($"Error during price import: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Parses the price information for a given uuid from both "paper" and "mtgo" formats.
        private static PriceList? ParsePriceList(JsonElement priceDataToken)
        {
            var priceList = new PriceList();

            // Process "paper" format prices
            if (priceDataToken.TryGetProperty("paper", out JsonElement paperData))
            {
                priceList.CardmarketNormal = GetDecimalValue(paperData, "cardmarket", "normal");
                priceList.CardmarketFoil = GetDecimalValue(paperData, "cardmarket", "foil");
                priceList.CardmarketEtched = GetDecimalValue(paperData, "cardmarket", "etched");

                priceList.CardhoarderNormal = GetDecimalValue(paperData, "cardhoarder", "normal");
                priceList.CardhoarderFoil = GetDecimalValue(paperData, "cardhoarder", "foil");
                priceList.CardhoarderEtched = GetDecimalValue(paperData, "cardhoarder", "etched");

                priceList.CardkingdomNormal = GetDecimalValue(paperData, "cardkingdom", "normal");
                priceList.CardkingdomFoil = GetDecimalValue(paperData, "cardkingdom", "foil");
                priceList.CardkingdomEtched = GetDecimalValue(paperData, "cardkingdom", "etched");

                priceList.TcgplayerNormal = GetDecimalValue(paperData, "tcgplayer", "normal");
                priceList.TcgplayerFoil = GetDecimalValue(paperData, "tcgplayer", "foil");
                priceList.TcgplayerEtched = GetDecimalValue(paperData, "tcgplayer", "etched");

                priceList.CardsphereNormal = GetDecimalValue(paperData, "cardsphere", "normal");
                priceList.CardsphereFoil = GetDecimalValue(paperData, "cardsphere", "foil");
                priceList.CardsphereEtched = GetDecimalValue(paperData, "cardsphere", "etched");
            }

            // Process "mtgo" format prices
            if (priceDataToken.TryGetProperty("mtgo", out JsonElement mtgoData))
            {
                priceList.CardhoarderNormal = GetDecimalValue(mtgoData, "cardhoarder", "normal");
                priceList.CardhoarderFoil = GetDecimalValue(mtgoData, "cardhoarder", "foil");
                priceList.CardhoarderEtched = GetDecimalValue(mtgoData, "cardhoarder", "etched");
            }

            return priceList;
        }

        // Helper method to retrieve decimal values from nested properties
        private static decimal? GetDecimalValue(JsonElement root, string retailer, string finishType)
        {
            if (root.TryGetProperty(retailer, out JsonElement retailerElement) &&
                retailerElement.TryGetProperty("retail", out JsonElement retailElement) &&
                retailElement.TryGetProperty(finishType, out JsonElement finishElement) &&
                finishElement.ValueKind == JsonValueKind.Number &&
                finishElement.TryGetDecimal(out decimal result))
            {
                return result;
            }

            return null;
        }


        // Update column headers 
        public static void UpdateDataGridHeaders(DataGrid dataGrid)
        {
            string currency;

            if (MainWindow.CurrentInstance.appsettingsRetailer == "cardmarket")
            {
                currency = "EUR";
            }
            else
            {
                currency = "USD";
            }

            // Find and update the column headers for "Price" and "Foil Price"
            foreach (var column in dataGrid.Columns)
            {
                // Check if the header is not null and is a string
                if (column.Header is string priceHeaderText && priceHeaderText.StartsWith("Price"))
                {
                    column.Header = $"Price ({currency})";
                }
                else if (column.Header is string foilPriceHeaderText && foilPriceHeaderText.StartsWith("Foil Price"))
                {
                    column.Header = $"Foil Price ({currency})";
                }
                else if (column.Header is string etchedPriceHeaderText && etchedPriceHeaderText.StartsWith("Etched Price"))
                {
                    column.Header = $"Etched Price ({currency})";
                }

            }
        }

    }
    public class PriceList
    {
        public string? Uuid { get; set; }
        public decimal? CardhoarderNormal { get; set; }
        public decimal? CardhoarderFoil { get; set; }
        public decimal? CardhoarderEtched { get; set; }
        public decimal? CardkingdomNormal { get; set; }
        public decimal? CardkingdomFoil { get; set; }
        public decimal? CardkingdomEtched { get; set; }
        public decimal? CardmarketNormal { get; set; }
        public decimal? CardmarketFoil { get; set; }
        public decimal? CardmarketEtched { get; set; }
        public decimal? CardsphereNormal { get; set; }
        public decimal? CardsphereFoil { get; set; }
        public decimal? CardsphereEtched { get; set; }
        public decimal? TcgplayerNormal { get; set; }
        public decimal? TcgplayerFoil { get; set; }
        public decimal? TcgplayerEtched { get; set; }
    }

}


