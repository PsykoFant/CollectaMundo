using CollectaMundo.DomainLogic.CardLegalities;
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
        public async Task<IReadOnlyList<CardLegalityFormatDbRow>> GetFormatCatalogAsync(SQLiteConnection conn, SQLiteTransaction? tx = null)
        {
            const string sql = """
                       SELECT
                           bitIndex,
                           value
                       FROM cardLegalityFormats
                       ORDER BY bitIndex;
                       """;

            using var command = SqliteDbHelpers.CreateCommand(conn, tx, sql);
            using var reader = await command.ExecuteReaderAsync();
            var bitIndexOrdinal = reader.GetOrdinal("bitIndex");
            var valueOrdinal = reader.GetOrdinal("value");
            var rows = new List<CardLegalityFormatDbRow>();

            while (await reader.ReadAsync())
            {
                rows.Add(new CardLegalityFormatDbRow
                {
                    BitIndex = reader.GetInt32(bitIndexOrdinal),
                    Value = reader.GetString(valueOrdinal)
                });
            }

            return rows;
        }
        public async Task RebuildFormatCatalogAsync(SQLiteConnection conn, SQLiteTransaction tx, IReadOnlyList<CardLegalityFormat> formats)
        {
            const string deleteSql =
                             """
                             DELETE FROM cardLegalityFormats;
                             """;

            using (var deleteCmd = SqliteDbHelpers.CreateCommand(conn, tx, deleteSql))
            {
                await deleteCmd.ExecuteNonQueryAsync();
            }

            const string insertSql =
                             """
                             INSERT INTO cardLegalityFormats (
                                 bitIndex,
                                 value
                             )
                             VALUES (
                                 @bitIndex,
                                 @value
                             );
                             """;

            using var insertCmd = SqliteDbHelpers.CreateCommand(conn, tx, insertSql);
            var bitIndexParameter = insertCmd.Parameters.Add("@bitIndex", System.Data.DbType.Int32);
            var valueParameter = insertCmd.Parameters.Add("@value", System.Data.DbType.String);

            foreach (var format in formats)
            {
                bitIndexParameter.Value = format.Id;
                valueParameter.Value = format.Value;

                await insertCmd.ExecuteNonQueryAsync();
            }
        }
    }
}
