using CollectaMundo.DomainLogic.CardPrices;
using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.CardPrices
{
    public class CardPriceRepository : ICardPriceRepository
    {
        public async Task InsertPricesInBatchesAsync(SQLiteConnection conn, SQLiteTransaction tx, string columnName, List<CardPrice> prices, int batchSize = 5000)
        {
            foreach (var batch in prices.Chunk(batchSize))
            {
                foreach (var price in batch)
                {
                    using var command = new SQLiteCommand($@"
                        INSERT INTO cardPrices (uuid, {columnName})
                        VALUES (@uuid, @price)
                        ON CONFLICT(uuid) DO UPDATE SET {columnName} = excluded.{columnName};",
                        conn,
                        tx);

                    command.Parameters.AddWithValue("@uuid", price.Uuid);
                    command.Parameters.AddWithValue("@price", price.Price);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }

    }
}
