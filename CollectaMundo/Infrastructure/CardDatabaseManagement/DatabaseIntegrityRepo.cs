using CollectaMundo.Infrastructure.CardDatabaseManagement.SqlDictionaries;
using System.Data.SQLite;
using System.IO;

namespace CollectaMundo.Infrastructure.CardDatabaseManagement
{
    public class DatabaseIntegrityRepo : IDatabaseIntegrityRepo
    {
        private static readonly List<string> RequiredObjects =
            [
                "cards",
                "myCollection",
                "oracleCards",
                "printingCards",
                "uniqueManaCostImages",
                "uniqueManaSymbols",
                "keyruneImages",
                "view_cardToken"
            ];
        public async Task<bool> HasExpectedTablesAndViewsAsync(SQLiteConnection conn)
        {
            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            await using var cmd = new SQLiteCommand("SELECT name FROM sqlite_master WHERE type IN ('table', 'view');", conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                existing.Add(reader.GetString(0));
            }

            return RequiredObjects.All(existing.Contains);
        }
        public async Task<bool> HasOracleCardsAsync(SQLiteConnection conn)
        {
            const string sql = """
                                SELECT EXISTS (
                                    SELECT 1 FROM oracleCards LIMIT 1
                                );
                                """;

            await using var cmd = new SQLiteCommand(sql, conn);

            var result = await cmd.ExecuteScalarAsync();

            return Convert.ToInt32(result) == 1;
        }
        public async Task VerifyMainDatabaseIntegrityAsync(SQLiteConnection conn, SQLiteTransaction? tx)
        {
            ArgumentNullException.ThrowIfNull(conn);

            const string sql = "PRAGMA main.integrity_check;";

            using var cmd = new SQLiteCommand(sql, conn, tx);

            var result = await cmd.ExecuteScalarAsync();
            var message = result?.ToString();

            if (!string.Equals(message, "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"SQLite integrity verification failed: {message ?? "No result returned."}");
            }
        }
        public async Task<bool> HasIncompletePreparationAsync(SQLiteConnection conn)
        {
            // Legacy databases do not have this table.
            const string tableExistsSql = """
                                        SELECT EXISTS (
                                            SELECT 1
                                            FROM main.sqlite_master
                                            WHERE type = 'table'
                                              AND name = 'databasePreparation'
                                        );
                                        """;

            using (var command = new SQLiteCommand(tableExistsSql, conn))
            {
                var exists = Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;

                if (!exists)
                {
                    // Existing legacy database: not explicitly incomplete.
                    return false;
                }
            }

            const string statusSql = """
                                    SELECT status
                                    FROM main.databasePreparation
                                    WHERE id = 1;
                                    """;

            using var statusCommand = new SQLiteCommand(statusSql, conn);

            var status = (await statusCommand.ExecuteScalarAsync())?.ToString();

            // Only an explicitly completed preparation is accepted. Missing rows or unexpected values are also rejected.
            return !string.Equals(status, "Complete", StringComparison.Ordinal);
        }
        public async Task MarkPreparationStartedAsync(SQLiteConnection conn, SQLiteTransaction tx)
        {
            // Create the table before the normal preparation pipeline begins. Reuse the application's authoritative schema definition.
            var createSql = DatabaseTableSql.GetAllStatements()["databasePreparation"];

            using (var createCommand = new SQLiteCommand(createSql, conn, tx))
            {
                await createCommand.ExecuteNonQueryAsync();
            }

            const string markSql = """
                                    INSERT INTO databasePreparation (id, status)
                                    VALUES (1, 'Preparing')
                                    ON CONFLICT(id)
                                    DO UPDATE SET status = 'Preparing';
                                    """;

            using var markCommand = new SQLiteCommand(markSql, conn, tx);

            await markCommand.ExecuteNonQueryAsync();
        }
        public async Task MarkPreparationCompletedAsync(SQLiteConnection conn, SQLiteTransaction tx)
        {
            const string sql = """
                                UPDATE databasePreparation
                                SET status = 'Complete'
                                WHERE id = 1
                                  AND status = 'Preparing';
                                """;

            using var command = new SQLiteCommand(sql, conn, tx);

            var affected = await command.ExecuteNonQueryAsync();

            if (affected != 1)
            {
                throw new InvalidDataException("Cannot mark database preparation complete: " + "no active preparation marker was found.");
            }
        }
    }
}
