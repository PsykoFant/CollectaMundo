using System.Data.SQLite;

namespace CollectaMundo.Data
{
    public interface ICardPriceRepository
    {
        Task InsertPricesInBatchesAsync(SQLiteConnection conn, string columnName, Dictionary<string, decimal> prices, int batchSize = 500);
    }

}
