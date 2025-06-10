using System.Data.SQLite;

namespace CollectaMundo.Data.CardPrices
{
    public interface ICardPriceRepository
    {
        Task InsertPricesInBatchesAsync(SQLiteConnection conn, string columnName, Dictionary<string, decimal> prices, int batchSize = 500);
    }

}
