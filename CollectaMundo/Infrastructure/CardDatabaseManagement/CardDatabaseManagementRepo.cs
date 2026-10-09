using CollectaMundo.ApplicationServices.Shared.Files;
using CollectaMundo.Infrastructure.CardDatabaseManagement.SqlDictionaries;
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace CollectaMundo.Infrastructure.CardDatabaseManagement
{
    public class CardDatabaseManagementRepo(ICsvFileWriter csvFileWriter) : ICardDatabaseManagementRepo
    {
        private readonly ICsvFileWriter _csvFileWriter = csvFileWriter;

        // Create
        public async Task CreateTablesAsync(SQLiteConnection conn, SQLiteTransaction tx)
        {
            var tables = DatabaseTableSql.GetAllStatements();

            foreach (var (name, sql) in tables)
            {
                try
                {
                    using var command = new SQLiteCommand(sql, conn, tx);
                    await command.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to create table '{name}'. SQL: {sql}", ex);
                }
            }

            Debug.WriteLine("Custom tables created successfully.");
        }
        public async Task CreateIndicesAsync(SQLiteConnection conn, SQLiteTransaction tx)
        {
            foreach (var (_, sql) in DatabaseIndexSql.Statements)
            {
                using var command = new SQLiteCommand(sql, conn, tx);
                await command.ExecuteNonQueryAsync();
            }

            Debug.WriteLine("Indices created successfully.");
        }
        public async Task CreateViewsAsync(SQLiteConnection conn, SQLiteTransaction tx)
        {
            foreach (var (name, sql) in DatabaseViewSql.Statements)
            {
                try
                {
                    using var cmd = new SQLiteCommand(sql, conn, tx);
                    await cmd.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to create view '{name}'.", ex);
                }
            }

            Debug.WriteLine("Views created successfully.");
        }
        public async Task OptimizeAsync(SQLiteConnection conn)
        {
            var commands = new[]
            {
                "VACUUM;",
                "ANALYZE;",
                "PRAGMA optimize;"
            };

            foreach (var cmdText in commands)
            {
                using var command = new SQLiteCommand(cmdText, conn);
                await command.ExecuteNonQueryAsync();
            }
            Debug.WriteLine("Database optimization completed.");
        }


        // Update
        public async Task<int> GetNumberOfSetsAsync(SQLiteConnection conn, CancellationToken ct = default)
        {
            const string sql = "SELECT COUNT(*) FROM sets;";

            using var command = new SQLiteCommand(sql, conn);

            var result = await command.ExecuteScalarAsync(ct);

            return Convert.ToInt32(result);
        }
        public async Task AttachTempDbAsync(SQLiteConnection conn, string newDbPath, IProgress<string> progress)
        {
            const string sql = "ATTACH DATABASE @path AS tempDb;";
            using var command = new SQLiteCommand(sql, conn);

            command.Parameters.AddWithValue("@path", newDbPath);

            await command.ExecuteNonQueryAsync();

            progress.Report(
                "Attached temp DB.");
        }
        public async Task ReplaceSourceTablesAsync(SQLiteConnection conn, SQLiteTransaction tx, IProgress<string> progress)
        {
            var existingSourceTables = await GetSourceTableNamesAsync(conn, tx, "main");
            var incomingSourceTables = await GetSourceTableDefinitionsAsync(conn, tx);

            Debug.WriteLine($"[CardDatabaseManagementRepo] Replacing source tables. Existing={existingSourceTables.Count}, Incoming={incomingSourceTables.Count}");

            // -------------------------------------------------
            // 1. Remove existing upstream tables
            // -------------------------------------------------

            progress.Report($"Removing {existingSourceTables.Count} old source tables...");

            foreach (var tableName in existingSourceTables)
            {
                var quotedTableName = QuoteIdentifier(tableName);
                var sql = $"DROP TABLE IF EXISTS {quotedTableName};";

                using var command = new SQLiteCommand(sql, conn, tx);

                await command.ExecuteNonQueryAsync();

                progress.Report($"Dropped {tableName}");

                Debug.WriteLine($"[CardDatabaseManagementRepo] Dropped {tableName}");
            }

            // -------------------------------------------------
            // 2. Recreate incoming tables using their original
            //    MTGJSON CREATE TABLE definitions
            // -------------------------------------------------

            progress.Report($"Creating {incomingSourceTables.Count} source tables...");

            foreach (var table in incomingSourceTables)
            {
                using var command = new SQLiteCommand(table.CreateSql, conn, tx);

                await command.ExecuteNonQueryAsync();

                progress.Report($"Created {table.Name}");

                Debug.WriteLine($"[CardDatabaseManagementRepo] Created {table.Name}");
            }

            // -------------------------------------------------
            // 3. Copy data into the recreated tables
            // -------------------------------------------------

            progress.Report($"Copying data for {incomingSourceTables.Count} source tables...");

            foreach (var table in incomingSourceTables)
            {
                var quotedTableName = QuoteIdentifier(table.Name);
                var sql = $"INSERT INTO {quotedTableName} " + $"SELECT * FROM tempDb.{quotedTableName};";

                using var command = new SQLiteCommand(sql, conn, tx);

                await command.ExecuteNonQueryAsync();

                progress.Report($"Copied {table.Name}");

                Debug.WriteLine($"[CardDatabaseManagementRepo] Copied {table.Name}");
            }

            progress.Report(
                "Source table replacement complete.");
        }
        public async Task DetachTempDbAsync(SQLiteConnection conn, IProgress<string> progress)
        {
            Debug.WriteLine("[CardDatabaseManagementRepo] Detaching temporary database...");

            using var command = new SQLiteCommand("DETACH DATABASE tempDb;", conn);

            await command.ExecuteNonQueryAsync();

            progress.Report("Detached temp DB.");
        }
        private static async Task<List<string>> GetSourceTableNamesAsync(SQLiteConnection conn, SQLiteTransaction tx, string databaseName)
        {
            // databaseName is supplied only internally as one of the known SQLite schema names: "main" or "tempDb".
            var quotedDatabaseName = QuoteIdentifier(databaseName);

            var sql =
                $"""
                 SELECT name
                 FROM {quotedDatabaseName}.sqlite_master
                 WHERE type = 'table'
                   AND name NOT LIKE 'sqlite_%'
                 ORDER BY name;
                 """;

            using var command = new SQLiteCommand(sql, conn, tx);
            using var reader = await command.ExecuteReaderAsync();
            var tableNames = new List<string>();

            while (await reader.ReadAsync())
            {
                var tableName = reader.GetString(0);

                if (DatabaseTableSql.CollectaMundoOwnedTableNames.Contains(tableName))
                {
                    continue;
                }

                tableNames.Add(tableName);
            }

            return tableNames;
        }
        private static async Task<List<SourceTableDefinition>> GetSourceTableDefinitionsAsync(SQLiteConnection conn, SQLiteTransaction tx)
        {
            const string sql = """
                       SELECT
                           name,
                           sql
                       FROM tempDb.sqlite_master
                       WHERE type = 'table'
                         AND name NOT LIKE 'sqlite_%'
                       ORDER BY name;
                       """;

            using var command = new SQLiteCommand(sql, conn, tx);
            using var reader = await command.ExecuteReaderAsync();
            var tables = new List<SourceTableDefinition>();

            while (await reader.ReadAsync())
            {
                var tableName = reader.GetString(0);

                if (DatabaseTableSql.CollectaMundoOwnedTableNames.Contains(tableName))
                {
                    continue;
                }

                if (reader.IsDBNull(1))
                {
                    throw new InvalidDataException($"Source table '{tableName}' has no CREATE TABLE SQL.");
                }

                var createSql = reader.GetString(1);

                tables.Add(new SourceTableDefinition(tableName, createSql));
            }

            return tables;
        }
        private sealed record SourceTableDefinition(
            string Name,
            string CreateSql);
        private static string QuoteIdentifier(string identifier)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

            return $"\"{identifier.Replace("\"", "\"\"")}\"";
        }

        // Export
        public async Task<string?> ExportCollectionAsync(SQLiteConnection conn, string backupFolderPath, CancellationToken ct = default)
        {
            Directory.CreateDirectory(backupFolderPath);

            using var command = new SQLiteCommand("SELECT * FROM myCollection", conn);
            using var reader = await command.ExecuteReaderAsync(ct);

            if (!reader.HasRows)
            {
                return null;
            }

            var filePath = Path.Combine(backupFolderPath, $"MyCollection_backup_{DateTime.Now:yyyyMMdd}.csv");
            var headers = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();

            await _csvFileWriter.WriteAsync(filePath, headers, ReadRowsAsync(reader, ct), ';', ct);

            return filePath;
        }
        private static async IAsyncEnumerable<IReadOnlyList<string?>> ReadRowsAsync(DbDataReader reader, [EnumeratorCancellation] CancellationToken ct)
        {
            while (await reader.ReadAsync(ct))
            {
                ct.ThrowIfCancellationRequested();

                var row = new string?[reader.FieldCount];

                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row[i] = reader.IsDBNull(i)
                        ? null
                        : reader.GetValue(i)?.ToString();
                }

                yield return row;
            }
        }
    }
}
