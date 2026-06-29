using CollectaMundo.Infrastructure.CardLegalities.Models.CollectaMundo.Infrastructure.CardLegalities.Models;
using CollectaMundo.Infrastructure.Shared;
using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.CardLegalities
{
    public sealed class CardLegalityRepo : ICardLegalityRepo
    {
        public async Task<IReadOnlyList<CardLegalityDbRow>> GetAllAsync(SQLiteConnection conn, SQLiteTransaction? tx = null)
        {
            const string sql = """
                                SELECT *
                                FROM cardLegalities;
                                """;

            using var cmd = DbHelpers.CreateCommand(conn, tx, sql);

            using var reader = await cmd.ExecuteReaderAsync();

            var rows = new List<CardLegalityDbRow>();

            while (await reader.ReadAsync())
            {
                var legalities = new Dictionary<string, string?>(
                    StringComparer.OrdinalIgnoreCase);

                string uuid = string.Empty;

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var column = reader.GetName(i);

                    if (column.Equals("uuid", StringComparison.OrdinalIgnoreCase))
                    {
                        uuid = reader.GetString(i);
                        continue;
                    }

                    legalities[column] =
                        reader.IsDBNull(i)
                            ? null
                            : reader.GetString(i);
                }

                rows.Add(new CardLegalityDbRow
                {
                    Uuid = uuid,
                    Legalities = legalities
                });
            }

            return rows;
        }
    }
}
