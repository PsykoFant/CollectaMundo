using CollectaMundo.ApplicationServices.Utilities;
using CollectaMundo.ApplicationServices.Utilities.Progress;
using CollectaMundo.Data.CardPrices;
using CollectaMundo.DomainLogic.CardPrices;
using System.Collections.Concurrent;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace CollectaMundo.ApplicationServices.CardPrices
{
    public class CardPriceService(IAppSettings appSettings, ICardPriceRepository cardPriceRepository) : ICardPriceService
    {
        private readonly IAppSettings _appSettings = appSettings;
        private readonly ICardPriceRepository _cardPriceRepository = cardPriceRepository;
        public async Task ImportPricesFromJsonAsync(string jsonPath, SQLiteConnection conn, IProgress<string>? statusProgress = null, IProgress<int>? percentProgress = null)
        {
            if (!File.Exists(jsonPath))
            {
                Debug.WriteLine($"[PriceImporter] Price file not found: {jsonPath}");
                throw new FileNotFoundException("Price JSON file not found.", jsonPath);
            }

            var effectiveProgress = percentProgress ?? new Progress<int>(_ => { }); // Use percentProgress if provided, otherwise use a no-op progress reporter
            var effectiveStatusProgress = statusProgress ?? new Progress<string>(_ => { });

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
            using var parseProgress = new ProgressReporter(effectiveProgress, allKeys.Count);
            var parsedPrices = new ConcurrentBag<CardPrice>();
            await Task.WhenAll(allKeys.Select(key =>
                Task.Run(async () =>
                {
                    await Task.Yield(); // Ensures this runs as a true async task
                    var prices = CardPriceJsonParser.ParsePricesForSource(root, key); // throws
                    foreach (var price in prices)
                    {
                        parsedPrices.Add(price);
                    }

                    parseProgress.Increment();
                })
            ));

            // Step 3: Group and insert into database (with progress)
            var groups = parsedPrices.GroupBy(p => $"{p.Retailer}{char.ToUpper(p.Finish[0]) + p.Finish[1..]}").ToList();
            using var insertProgress = new ProgressReporter(effectiveProgress, groups.Count);

            foreach (var group in groups)
            {
                string tableName = group.Key;
                var priceList = group.Select(p => new CardPrice { Uuid = p.Uuid, Price = p.Price }).ToList();
                await _cardPriceRepository.InsertPricesInBatchesAsync(conn, tableName, priceList);

                var retailer = group.First().Retailer;
                var finish = group.First().Finish;
                effectiveStatusProgress.Report($"Imported retailer {retailer} prices for card finish: {finish} ...");

                insertProgress.Increment();

                // Force a UI render between each insert
                await UIHelper.ForceRenderAsync();
            }

            // Step 4: Update settings with the JSON's actual date
            _appSettings.UpdatePriceInfo(jsonDate, "all");
        }
    }
}



