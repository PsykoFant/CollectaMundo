using System.Data.SQLite;
using System.Text;

namespace CollectaMundo.Data
{
    public class CardPriceRepository : ICardPriceRepository
    {
        public async Task InsertPricesInBatchesAsync(SQLiteConnection conn, string columnName, Dictionary<string, decimal> prices, int batchSize = 500)
        {
            var batches = prices
                .Select((pair, index) => new { pair.Key, pair.Value, Index = index })
                .GroupBy(x => x.Index / batchSize);

            foreach (var batch in batches)
            {
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

                // Remove trailing comma
                queryBuilder.Length--;

                // Add ON CONFLICT clause
                queryBuilder.Append($" ON CONFLICT(uuid) DO UPDATE SET {columnName} = excluded.{columnName};");

                using var cmd = new SQLiteCommand(queryBuilder.ToString(), conn);
                cmd.Parameters.AddRange(parameters.ToArray());

                await cmd.ExecuteNonQueryAsync();
            }
        }

    }
}
