using CollectaMundo.Data;
using CollectaMundo.DomainLogic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace CollectaMundo.ApplicationServices
{
    public class CardPriceImporter(IAppSettings appSettings, IDbConnectionFactory dbFactory, ICardPriceRepository cardPriceRepository) : ICardPriceImporter
    {
        private readonly IAppSettings _appSettings = appSettings;
        private readonly IDbConnectionFactory _dbFactory = dbFactory;
        private readonly ICardPriceRepository _cardPriceRepository = cardPriceRepository ?? throw new ArgumentNullException(nameof(cardPriceRepository));

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

                var normalPrices = CardPriceJsonParser.ParsePriceList(pricesRoot, $"{retailer}Normal");
                var foilPrices = CardPriceJsonParser.ParsePriceList(pricesRoot, $"{retailer}Foil");
                var etchedPrices = CardPriceJsonParser.ParsePriceList(pricesRoot, $"{retailer}Etched");

                Debug.WriteLine($"[PriceImporter] Parsed {normalPrices.Count} normal, {foilPrices.Count} foil, {etchedPrices.Count} etched prices.");

                await _cardPriceRepository.InsertPricesInBatchesAsync(conn, $"{retailer}Normal", normalPrices);
                await _cardPriceRepository.InsertPricesInBatchesAsync(conn, $"{retailer}Foil", foilPrices);
                await _cardPriceRepository.InsertPricesInBatchesAsync(conn, $"{retailer}Etched", etchedPrices);

                Debug.WriteLine("[PriceImporter] Price import completed.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PriceImporter] Error importing prices: {ex.Message}");
            }
        }


        private static async Task UpdatePriceColumnAsync(SQLiteConnection conn, string tableName, string columnName, string uuid, decimal price)
        {
            string query = $@"
                INSERT INTO {tableName} (uuid, {columnName})
                VALUES (@uuid, @price)
                ON CONFLICT(uuid) DO UPDATE SET {columnName} = excluded.{columnName};";

            using var cmd = new SQLiteCommand(query, conn);
            cmd.Parameters.AddWithValue("@uuid", uuid);
            cmd.Parameters.AddWithValue("@price", price);

            await cmd.ExecuteNonQueryAsync();
        }
    }
}


