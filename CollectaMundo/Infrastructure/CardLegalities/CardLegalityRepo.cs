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

            var uuidOrdinal = reader.GetOrdinal("uuid");
            var legalityColumns = Enumerable.Range(0, reader.FieldCount).Where(i => i != uuidOrdinal).Select(i => (Ordinal: i, Name: reader.GetName(i))).ToArray();
            var rows = new List<CardLegalityDbRow>(capacity: 112000);

            while (await reader.ReadAsync())
            {
                var uuid = reader.GetString(uuidOrdinal);

                var legalities = new Dictionary<string, string?>(capacity: legalityColumns.Length, comparer: StringComparer.OrdinalIgnoreCase);

                foreach (var (ordinal, name) in legalityColumns)
                {
                    legalities[name] = reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
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
