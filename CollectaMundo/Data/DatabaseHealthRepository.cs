using System.Data.SQLite;

namespace CollectaMundo.Data
{
    public class DatabaseHealthRepository : IDatabaseHealthRepository
    {
        private static readonly List<string> RequiredObjects =
        [
            "cards", "myCollection", "uniqueManaCostImages", "uniqueManaSymbols",
            "keyruneImages", "view_allCards", "view_myCollection", "view_cardToken"
        ];
        public async Task<bool> HasExpectedTablesAndViewsAsync(SQLiteConnection conn)
        {
            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using var cmd = new SQLiteCommand("SELECT name FROM sqlite_master WHERE type IN ('table', 'view');", conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                existing.Add(reader.GetString(0));
            }

            return RequiredObjects.All(existing.Contains);
        }

        public async Task<bool> QuickCheckAsync(SQLiteConnection conn)
        {
            using var cmd = new SQLiteCommand("PRAGMA quick_check;", conn);
            var result = await cmd.ExecuteScalarAsync();
            return result?.ToString() == "ok";
        }
    }

}
