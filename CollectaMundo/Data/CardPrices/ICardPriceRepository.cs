using CollectaMundo.DomainLogic.CardPrices;
using System.Data.SQLite;

namespace CollectaMundo.Data.CardPrices
{
    public interface ICardPriceRepository
    {
        Task InsertPricesInBatchesAsync(SQLiteConnection conn, string columnName, List<CardPrice> prices, int batchSize = 5000);
    }
}
