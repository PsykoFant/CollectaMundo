using CollectaMundo.ApplicationServices.Utilities;
using CollectaMundo.Data.CardPrices;
using CollectaMundo.DomainLogic.CardPrices;
using CollectaMundo.ViewModels;
using System.Collections.Concurrent;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace CollectaMundo.ApplicationServices.CardPrices
{
    public class CardPriceService(IAppSettings appSettings, ICardPriceRepository cardPriceRepository, StatusViewModel statusVM) : ICardPriceService
    {
        private readonly IAppSettings _appSettings = appSettings;
        private readonly ICardPriceRepository _cardPriceRepository = cardPriceRepository;
        private readonly StatusViewModel _statusVM = statusVM;
        public async Task ImportPricesFromJsonAsync(string jsonPath, SQLiteConnection conn)
        {
            if (!File.Exists(jsonPath))
            {
                Debug.WriteLine($"[PriceImporter] Price file not found: {jsonPath}");
                return;
            }

            try
            {
                // Step 1: Load and parse JSON
                using var stream = File.OpenRead(jsonPath);
                var jsonDoc = await JsonDocument.ParseAsync(stream);
                var root = jsonDoc.RootElement;

                string? jsonDate = root.GetProperty("meta").GetProperty("date").GetString();
                if (jsonDate == null)
                {
                    Debug.WriteLine("[PriceImporter] Missing date in price JSON metadata.");
                    return;
                }

                // Step 2: Parse all prices (with progress)
                var allKeys = CardPriceDefinitions.GetAllKeys().ToList();
                using var parseProgress = new ProgressReporter(_statusVM, allKeys.Count);
                var parsedPrices = new ConcurrentBag<CardPrice>();

                await Task.WhenAll(allKeys.Select(key =>
                    Task.Run(() =>
                    {
                        var prices = CardPriceJsonParser.ParsePricesForSource(root, key);
                        foreach (var price in prices)
                            parsedPrices.Add(price);
                        parseProgress.Increment();
                    })
                ));

                // Step 3: Group and insert into database (with progress)
                var groups = parsedPrices.GroupBy(p => $"{p.Retailer}{char.ToUpper(p.Finish[0]) + p.Finish[1..]}").ToList();

                using var insertProgress = new ProgressReporter(_statusVM, groups.Count);

                foreach (var group in groups)
                {
                    string tableName = group.Key;
                    var priceList = group.Select(p => new CardPrice { Uuid = p.Uuid, Price = p.Price }).ToList();
                    await _cardPriceRepository.InsertPricesInBatchesAsync(conn, tableName, priceList);

                    var retailer = group.First().Retailer;
                    var finish = group.First().Finish;
                    _statusVM.StatusLabelMain = $"Imported retailer {retailer} prices for card finish: {finish} ...";

                    insertProgress.Increment();

                    // Force a UI render between each insert
                    await UIHelper.ForceRenderAsync();
                }

                // Step 4: Update settings with the JSON's actual date
                _appSettings.UpdatePriceInfo(jsonDate, "all");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PriceImporter] Error importing prices: {ex.Message}");
            }
        }


    }
}


