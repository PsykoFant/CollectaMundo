using CollectaMundo.Data.Common;
using System.Data.SQLite;

namespace CollectaMundo.Data.UpdateDB
{
    public class UpdateDbRepo() : IUpdateDbRepo
    {
        public async Task<int> GetNumberOfSetsAsync(SQLiteConnection conn)
        {
            var sets = await DbHelpers.GetUniqueValuesAsync(conn, "sets", "code");
            return sets.Count;
        }
        public async Task CopyTablesFromNewDbAsync(SQLiteConnection conn, IProgress<string> progress, string newDbPath)
        {
            var tables = new Dictionary<string, string>
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

            // Drop
            foreach (var item in tables)
            {
                using var dropCommand = new SQLiteCommand(item.Value, conn);
                await dropCommand.ExecuteNonQueryAsync();
                progress.Report($"Dropped {item.Key}");
            }

            // Attach
            var attachSql = $"ATTACH DATABASE '{newDbPath}' AS tempDb;";
            await new SQLiteCommand(attachSql, conn).ExecuteNonQueryAsync();
            progress.Report("Attached temp DB.");

            // Copy
            foreach (var item in tables)
            {
                var copySql = $"CREATE TABLE {item.Key} AS SELECT * FROM tempDb.{item.Key};";
                using var copyCommand = new SQLiteCommand(copySql, conn);
                await copyCommand.ExecuteNonQueryAsync();
                progress.Report($"Copied {item.Key}");
            }

            // Detach
            var detachSql = "DETACH DATABASE tempDb;";
            await new SQLiteCommand(detachSql, conn).ExecuteNonQueryAsync();
            progress.Report("Detached temp DB.");
        }

    }
}
