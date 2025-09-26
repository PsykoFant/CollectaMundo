using CollectaMundo.DomainLogic.CardPrices;
using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.CardPrices
{
    public class CardPriceRepository : ICardPriceRepository
    {
        public async Task InsertPricesInBatchesAsync(SQLiteConnection conn, string columnName, List<CardPrice> prices, int batchSize = 5000)
        {
            foreach (var batch in prices.Chunk(batchSize))
            {
                using var transaction = conn.BeginTransaction();
                foreach (var price in batch)
                {
                    var command = conn.CreateCommand();
                    command.CommandText = $@"
                        INSERT INTO cardPrices (uuid, {columnName})
                        VALUES (@uuid, @price)
                        ON CONFLICT(uuid) DO UPDATE SET {columnName} = excluded.{columnName};";

                    command.Parameters.AddWithValue("@uuid", price.Uuid);
                    command.Parameters.AddWithValue("@price", price.Price);
                    await command.ExecuteNonQueryAsync();
                }
                transaction.Commit();
            }
        }

    }
}
