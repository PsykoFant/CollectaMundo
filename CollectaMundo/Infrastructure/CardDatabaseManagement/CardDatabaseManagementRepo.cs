using CollectaMundo.Infrastructure.CardDatabaseManagement.SqlDictionaries;
using CollectaMundo.Infrastructure.Shared;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace CollectaMundo.Infrastructure.CardDatabaseManagement
{
    public class CardDatabaseManagementRepo : ICardDatabaseManagementRepo
    {
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
            var sets = await DbHelpers.GetUniqueValuesAsync(conn, "sets", "code", null, ct);
            return sets.Count;
        }
        public async Task AttachTempDbAsync(SQLiteConnection conn, string newDbPath, IProgress<string> progress)
        {
            var attachSql = $"ATTACH DATABASE '{newDbPath}' AS tempDb;";
            await new SQLiteCommand(attachSql, conn).ExecuteNonQueryAsync();
            progress.Report("Attached temp DB.");
        }
        public async Task DropTablesAsync(SQLiteConnection conn, IProgress<string> progress)
        {
            var tables = TablesToCopy;

            Debug.WriteLine("Dropping old tables...");
            foreach (var item in tables)
            {
                using var dropCommand = new SQLiteCommand(item.Value, conn);
                await dropCommand.ExecuteNonQueryAsync();
                progress.Report($"Dropped {item.Key}");
            }
        }
        public async Task CopyTablesAsync(SQLiteConnection conn, IProgress<string> progress)
        {
            var tables = TablesToCopy;

            Debug.WriteLine("Copying tables...");
            foreach (var item in tables)
            {
                var copySql = $"CREATE TABLE {item.Key} AS SELECT * FROM tempDb.{item.Key};";
                using var copyCommand = new SQLiteCommand(copySql, conn);
                await copyCommand.ExecuteNonQueryAsync();
                progress.Report($"Copied {item.Key}");
                Debug.WriteLine($"Copied {item.Key}");
            }

            progress.Report("Copy complete...");

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        public async Task DetachTempDbAsync(SQLiteConnection conn, IProgress<string> progress)
        {
            Debug.WriteLine("Detaching new DB...");
            var detachSql = "DETACH DATABASE tempDb;";
            await new SQLiteCommand(detachSql, conn).ExecuteNonQueryAsync();
            progress.Report("Detached temp DB.");
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

            string filePath = Path.Combine(backupFolderPath, $"MyCollection_backup_{DateTime.Now:yyyyMMdd}.csv");
            using var writer = new StreamWriter(filePath, false, Encoding.UTF8);

            // Write header
            for (int i = 0; i < reader.FieldCount; i++)
            {
                ct.ThrowIfCancellationRequested();

                writer.Write(reader.GetName(i));
                if (i < reader.FieldCount - 1)
                {
                    writer.Write(";");
                }
            }
            writer.WriteLine();

            // Write rows
            while (await reader.ReadAsync(ct))
            {
                ct.ThrowIfCancellationRequested();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    string value = reader[i]?.ToString()?.Replace(";", ",") ?? string.Empty;
                    writer.Write(value);
                    if (i < reader.FieldCount - 1)
                    {
                        writer.Write(";");
                    }
                }
                writer.WriteLine();
            }

            return filePath;
        }

        // Helper
        private static readonly Dictionary<string, string> TablesToCopy = new()
            {
                {"cardForeignData", "DROP TABLE IF EXISTS cardForeignData;" },
                {"cardIdentifiers", "DROP TABLE IF EXISTS cardIdentifiers;" },
                {"cardLegalities", "DROP TABLE IF EXISTS cardLegalities;" },
                {"cardPurchaseUrls", "DROP TABLE IF EXISTS cardPurchaseUrls;" },
                {"cardRulings", "DROP TABLE IF EXISTS cardRulings;" },
                {"cards", "DROP TABLE IF EXISTS cards;" },
                {"meta", "DROP TABLE IF EXISTS meta;" },
                {"setBoosterContentWeights", "DROP TABLE IF EXISTS setBoosterContentWeights;" },
                {"setBoosterContents", "DROP TABLE IF EXISTS setBoosterContents;" },
                {"setBoosterSheetCards", "DROP TABLE IF EXISTS setBoosterSheetCards;" },
                {"setBoosterSheets", "DROP TABLE IF EXISTS setBoosterSheets;" },
                {"setTranslations", "DROP TABLE IF EXISTS setTranslations;" },
                {"sets", "DROP TABLE IF EXISTS sets;" },
                {"tokenIdentifiers", "DROP TABLE IF EXISTS tokenIdentifiers;" },
                {"tokens", "DROP TABLE IF EXISTS tokens;" },
            };

    }
}
