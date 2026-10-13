using CollectaMundo.Infrastructure.CardLegalities.Models;
using CollectaMundo.Infrastructure.Shared.Database;
using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.CardLegalities
{
    public sealed class CardLegalityRepo : ICardLegalityRepo
    {
        public async Task<CardLegalityReadResult> GetAllAsync(SQLiteConnection conn, SQLiteTransaction? tx = null)
        {
            const string sql = """
                           SELECT *
                           FROM cardLegalities;
                           """;

            using var cmd = SqliteDbHelpers.CreateCommand(conn, tx, sql);
            using var reader = await cmd.ExecuteReaderAsync();
            var uuidOrdinal = reader.GetOrdinal("uuid");
            var legalityColumns = Enumerable.Range(0, reader.FieldCount).Where(i => i != uuidOrdinal).Select(i => (Ordinal: i, Name: reader.GetName(i))).ToArray();
            var formatNames = legalityColumns.Select(x => x.Name).ToArray();
            var rows = new List<CardLegalityDbRow>(capacity: 112_000);

            while (await reader.ReadAsync())
            {
                var statuses = new string?[legalityColumns.Length];

                for (var i = 0; i < legalityColumns.Length; i++)
                {
                    var ordinal = legalityColumns[i].Ordinal;

                    statuses[i] = reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
                }

                rows.Add(
                    new CardLegalityDbRow
                    {
                        Uuid = reader.GetString(uuidOrdinal),
                        Statuses = statuses
                    });
            }

            return new CardLegalityReadResult
            {
                FormatNames = formatNames,
                Rows = rows
            };
        }
    }
}
