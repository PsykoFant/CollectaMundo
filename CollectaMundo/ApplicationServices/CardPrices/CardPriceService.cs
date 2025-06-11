using CollectaMundo.Data;
using CollectaMundo.Data.CardPrices;
using CollectaMundo.DomainLogic.CardPrices;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace CollectaMundo.ApplicationServices.CardPrices
{
    public class CardPriceService : ICardPriceService
    {
        private readonly IAppSettings _appSettings;
        private readonly ICardPriceRepository _cardPriceRepository;
        private readonly IDbConnectionFactory _dbFactory;

        public CardPriceService(IAppSettings appSettings, ICardPriceRepository cardPriceRepository)
        {
            _appSettings = appSettings;
            _cardPriceRepository = cardPriceRepository;
            _dbFactory = new DbConnectionFactory(_appSettings);
        }

        public async Task ImportPricesFromJsonAsync(string jsonPath, SQLiteConnection conn)
        {
            if (!File.Exists(jsonPath))
            {
                Debug.WriteLine($"[PriceImporter] Price file not found: {jsonPath}");
                return;
            }
            try
            {
                using var stream = File.OpenRead(jsonPath);
                var jsonDoc = await JsonDocument.ParseAsync(stream);
                var pricesRoot = jsonDoc.RootElement;

                string retailer = _appSettings.PriceInfo.Retailer;


                var normalTask = Task.Run(() => CardPriceJsonParser.ParsePriceList(pricesRoot, "paper", retailer, "normal"));
                var foilTask = Task.Run(() => CardPriceJsonParser.ParsePriceList(pricesRoot, "paper", retailer, "foil"));
                var etchedTask = Task.Run(() => CardPriceJsonParser.ParsePriceList(pricesRoot, "paper", retailer, "etched"));

                await Task.WhenAll(normalTask, foilTask, etchedTask);

                var normalPrices = normalTask.Result;
                var foilPrices = foilTask.Result;
                var etchedPrices = etchedTask.Result;

                await _cardPriceRepository.InsertPricesInBatchesAsync(conn, $"{retailer}Normal", normalPrices);
                await _cardPriceRepository.InsertPricesInBatchesAsync(conn, $"{retailer}Foil", foilPrices);
                await _cardPriceRepository.InsertPricesInBatchesAsync(conn, $"{retailer}Etched", etchedPrices);

                _appSettings.UpdatePriceInfo(DateTime.UtcNow.ToString("yyyy-MM-dd"), retailer);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PriceImporter] Error importing prices: {ex.Message}");
            }
        }
    }
}


