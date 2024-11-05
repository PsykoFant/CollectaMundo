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

                        if (await DownloadAndPrepDB.DownloadResourceFileIfNotExistAsync(pricesDownloadsPath, DownloadAndPrepDB.pricesDownloadUrl, "Updating card prices - please wait...", "price file...", true, true))
                        {
                            StatusMessageUpdated?.Invoke("Updating card prices ...");
                            await DBAccess.OpenConnectionAsync();

                            await Task.Run(() => ImportPricesFromJsonAsync(20000));

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
                string createdAt = jsonObject["meta"]?["date"]?.ToString()
                                  ?? throw new InvalidOperationException("Meta:date not found in JSON.");

                var priceData = jsonObject["data"] as JObject;
                if (priceData == null || !priceData.HasValues)
                {
                    throw new InvalidOperationException("No price data found in the JSON file.");
                }

                // Prepare SQL statement for batch insertion
                string insertSql = @"
                    INSERT OR REPLACE INTO cardPrices (uuid, cardhoarderNormal, cardhoarderFoil, cardhoarderEtched, 
                        cardkingdomNormal, cardkingdomFoil, cardkingdomEtched, cardmarketNormal, cardmarketFoil, cardmarketEtched, 
                        cardsphereNormal, cardsphereFoil, cardsphereEtched, tcgplayerNormal, tcgplayerFoil, tcgplayerEtched)
                    VALUES (@uuid, @cardhoarderNormal, @cardhoarderFoil, @cardhoarderEtched, 
                        @cardkingdomNormal, @cardkingdomFoil, @cardkingdomEtched, @cardmarketNormal, @cardmarketFoil, @cardmarketEtched, 
                        @cardsphereNormal, @cardsphereFoil, @cardsphereEtched, @tcgplayerNormal, @tcgplayerFoil, @tcgplayerEtched);";

                var transaction = DBAccess.connection.BeginTransaction();
                try
                {
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

                    int counter = 0;

                    // Iterate through the price data in the JSON
                    foreach (var token in priceData)
                    {
                        string uuid = token.Key;
                        if (token.Value != null)
                        {
                            var priceList = ParsePriceList(token.Value);

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

                                if (++counter % batchSize == 0)
                                {
                                    await transaction.CommitAsync();
                                    transaction.Dispose();
                                    transaction = DBAccess.connection.BeginTransaction();
                                }
                            }
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

                // Clean up the JSON file after processing
                //File.Delete(jsonFilePath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during price import: {ex.Message}");
                MessageBox.Show($"Error during price import: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Parses the price information for a given uuid from both "paper" and "mtgo" formats.
        private static PriceList? ParsePriceList(JToken priceDataToken)
        {
            var priceList = new PriceList();

            // Process "paper" format prices
            var paperData = priceDataToken["paper"];
            if (paperData != null)
            {
                priceList.CardmarketNormal = paperData["cardmarket"]?["retail"]?["normal"]?.Values().FirstOrDefault()?.Value<decimal?>();
                priceList.CardmarketFoil = paperData["cardmarket"]?["retail"]?["foil"]?.Values().FirstOrDefault()?.Value<decimal?>();
                priceList.CardmarketEtched = paperData["cardmarket"]?["retail"]?["etched"]?.Values().FirstOrDefault()?.Value<decimal?>();

                priceList.CardhoarderNormal = paperData["cardhoarder"]?["retail"]?["normal"]?.Values().FirstOrDefault()?.Value<decimal?>();
                priceList.CardhoarderFoil = paperData["cardhoarder"]?["retail"]?["foil"]?.Values().FirstOrDefault()?.Value<decimal?>();
                priceList.CardhoarderEtched = paperData["cardhoarder"]?["retail"]?["etched"]?.Values().FirstOrDefault()?.Value<decimal?>();

                priceList.CardkingdomNormal = paperData["cardkingdom"]?["retail"]?["normal"]?.Values().FirstOrDefault()?.Value<decimal?>();
                priceList.CardkingdomFoil = paperData["cardkingdom"]?["retail"]?["foil"]?.Values().FirstOrDefault()?.Value<decimal?>();
                priceList.CardkingdomEtched = paperData["cardkingdom"]?["retail"]?["etched"]?.Values().FirstOrDefault()?.Value<decimal?>();

                priceList.TcgplayerNormal = paperData["tcgplayer"]?["retail"]?["normal"]?.Values().FirstOrDefault()?.Value<decimal?>();
                priceList.TcgplayerFoil = paperData["tcgplayer"]?["retail"]?["foil"]?.Values().FirstOrDefault()?.Value<decimal?>();
                priceList.TcgplayerEtched = paperData["tcgplayer"]?["retail"]?["etched"]?.Values().FirstOrDefault()?.Value<decimal?>();

                priceList.CardsphereNormal = paperData["cardsphere"]?["retail"]?["normal"]?.Values().FirstOrDefault()?.Value<decimal?>();
                priceList.CardsphereFoil = paperData["cardsphere"]?["retail"]?["foil"]?.Values().FirstOrDefault()?.Value<decimal?>();
                priceList.CardsphereEtched = paperData["cardsphere"]?["retail"]?["etched"]?.Values().FirstOrDefault()?.Value<decimal?>();
            }

            // Process "mtgo" format prices
            var mtgoData = priceDataToken["mtgo"];
            if (mtgoData != null)
            {
                priceList.CardhoarderNormal = mtgoData["cardhoarder"]?["retail"]?["normal"]?.Values().FirstOrDefault()?.Value<decimal?>();
                priceList.CardhoarderFoil = mtgoData["cardhoarder"]?["retail"]?["foil"]?.Values().FirstOrDefault()?.Value<decimal?>();
                priceList.CardhoarderEtched = mtgoData["cardhoarder"]?["retail"]?["etched"]?.Values().FirstOrDefault()?.Value<decimal?>();
            }

            return priceList;
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


