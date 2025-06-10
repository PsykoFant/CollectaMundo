using System.Data.SQLite;
using System.Text;

namespace CollectaMundo.Data.CardPrices
{
    public class CardPriceRepository : ICardPriceRepository
    {
        public async Task InsertPricesInBatchesAsync(SQLiteConnection conn, string columnName, Dictionary<string, decimal> prices, int batchSize = 5000)
        {
            var batches = prices
                .Select((pair, index) => new { pair.Key, pair.Value, Index = index })
                .GroupBy(x => x.Index / batchSize);

            foreach (var batch in batches)
            {
                using var tx = conn.BeginTransaction(); // optional if not in higher-level transaction

                var queryBuilder = new StringBuilder();
                queryBuilder.Append($"INSERT INTO cardPrices (uuid, {columnName}) VALUES ");

                var parameters = new List<SQLiteParameter>();
                int paramIndex = 0;

                foreach (var item in batch)
                {
                    string uuidParam = $"@uuid{paramIndex}";
                    string priceParam = $"@price{paramIndex}";

                    queryBuilder.Append($"({uuidParam}, {priceParam}),");

                    parameters.Add(new SQLiteParameter(uuidParam, item.Key));
                    parameters.Add(new SQLiteParameter(priceParam, item.Value));

                    paramIndex++;
                }

                queryBuilder.Length--; // remove trailing comma
                queryBuilder.Append($" ON CONFLICT(uuid) DO UPDATE SET {columnName} = excluded.{columnName};");

                using var cmd = new SQLiteCommand(queryBuilder.ToString(), conn, tx);
                cmd.Parameters.AddRange(parameters.ToArray());

                await cmd.ExecuteNonQueryAsync();
                tx.Commit();
            }

        }

    }
}
