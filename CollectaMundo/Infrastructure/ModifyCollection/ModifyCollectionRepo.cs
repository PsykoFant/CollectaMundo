using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.ModifyCollection
{
    public class ModifyCollectionRepo() : IModifyCollectionRepo
    {
        // Lookups
        public async Task<List<string>> FetchLanguagesForCardAsync(string uuid, SQLiteConnection conn)
        {
            if (string.IsNullOrEmpty(uuid))
            {
                return ["English"];
            }

            const string sql = """
                                SELECT language
                                FROM cardForeignData
                                WHERE uuid = @uuid

                                UNION

                                SELECT language
                                FROM printingCards
                                WHERE uuid = @uuid;
                                """;

            var languages = new List<string>();

            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@uuid", uuid);

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                string? language = reader["language"]?.ToString();

                if (!string.IsNullOrEmpty(language))
                {
                    languages.Add(language);
                }
            }

            return languages;
        }
        public async Task<List<string>> FetchFinishesForCardAsync(string uuid, SQLiteConnection conn)
        {
            const string sql = """
                            SELECT finishes
                            FROM printingCards
                            WHERE uuid = @uuid;
                            """;

            var finishes = new List<string>();

            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@uuid", uuid);

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var finish = reader["finishes"]?.ToString();

                if (!string.IsNullOrEmpty(finish))
                {
                    finishes.AddRange(finish.Split(',').Select(f => f.Trim()));
                }
            }

            // Remove unwanted finish types and resolve conflicts.
            finishes = [.. finishes.Distinct().Where(f => !f.Equals("signed", StringComparison.OrdinalIgnoreCase))];

            if (finishes.Contains("foil", StringComparer.OrdinalIgnoreCase) && finishes.Contains("etched", StringComparer.OrdinalIgnoreCase))
            {
                finishes = [.. finishes.Where(f => !f.Equals("foil", StringComparison.OrdinalIgnoreCase))];
            }

            return finishes;
        }
    }
}
