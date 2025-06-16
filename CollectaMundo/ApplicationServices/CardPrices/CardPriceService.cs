using CollectaMundo.Data.CardPrices;
using CollectaMundo.DomainLogic.CardPrices;
using CollectaMundo.ViewModels;
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
                Stopwatch stopwatch = Stopwatch.StartNew();

                using var stream = File.OpenRead(jsonPath);
                var jsonDoc = await JsonDocument.ParseAsync(stream);
                var root = jsonDoc.RootElement;

                // Extract price data date from metadata
                string date = root.GetProperty("meta").GetProperty("date").GetString()
                              ?? DateTime.UtcNow.ToString("yyyy-MM-dd");

                stopwatch.Stop();
                Debug.WriteLine($"[PriceImporter] JSON loaded and metadata parsed in {stopwatch.ElapsedMilliseconds} ms.");
                stopwatch.Restart();

                // Parse all prices
                List<CardPrice> allPrices = await CardPriceJsonParser.ParseAllPricesAsync(root);
                stopwatch.Stop();
                Debug.WriteLine($"[PriceImporter] Parsed {allPrices.Count} prices in {stopwatch.ElapsedMilliseconds} ms.");

                // Group and persist prices
                stopwatch.Restart();
                var grouped = allPrices.GroupBy(p => (p.Retailer, p.Finish)).ToList();

                foreach (var group in grouped)
                {
                    string tableName = $"{group.Key.Retailer}{Capitalize(group.Key.Finish)}";
                    await _cardPriceRepository.InsertPricesInBatchesAsync(conn, tableName, group.ToList());
                    Debug.WriteLine($"[PriceImporter] Inserted {group.Count()} prices into {tableName}");
                }

                stopwatch.Stop();
                Debug.WriteLine($"[PriceImporter] All prices inserted in {stopwatch.ElapsedMilliseconds} ms.");

                // Update price info timestamp
                _appSettings.UpdatePriceInfo(date, "all-retailers");

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PriceImporter] Error importing prices: {ex.Message}");
            }

            static string Capitalize(string input) => string.IsNullOrEmpty(input) ? input : char.ToUpperInvariant(input[0]) + input[1..];
        }

    }
}


