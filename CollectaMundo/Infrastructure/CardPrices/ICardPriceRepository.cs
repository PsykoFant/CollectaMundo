using CollectaMundo.DomainLogic.CardPrices;
using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.CardPrices
{
    public interface ICardPriceRepository
    {
        Task InsertPricesInBatchesAsync(SQLiteConnection conn, SQLiteTransaction tx, string columnName, List<CardPrice> prices, int batchSize = 5000);
    }
}
