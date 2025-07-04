using CollectaMundo.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;

namespace CollectaMundo.Data.UpdateDB
{
    public class UpdateDbRepo() : IUpdateDbRepo
    {
        public async Task<int> GetNumberOfSetsAsync(SQLiteConnection conn)
        {
            var sets = await DbHelpers.GetUniqueValuesAsync(conn, "sets", "code");
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
            }

            progress.Report("Copy complete. Finalizing handles...");

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
