using System.Data.SQLite;
using System.Diagnostics;

namespace CollectaMundo.Infrastructure.EditCollection
{
    public class EditCollectionRepo() : IEditCollectionRepo
    {

        // Lookups
        public async Task<List<string>> FetchLanguagesForCardAsync(string uuid, SQLiteConnection conn)
        {
            if (string.IsNullOrEmpty(uuid))
            {
                return ["English"];
            }

            var languages = new List<string>();
            string query = @"
                SELECT language FROM cardForeignData WHERE uuid = @uuid
                UNION
                SELECT language FROM cards WHERE uuid = @uuid
                UNION
                SELECT language FROM tokens WHERE uuid = @uuid";
            try
            {
                using var command = new SQLiteCommand(query, conn);
                command.Parameters.AddWithValue("@uuid", uuid);
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string? language = reader["language"] as string;
                    if (!string.IsNullOrEmpty(language))
                    {
                        languages.Add(language);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in FetchLanguagesForCardAsync: {ex.Message}");
                throw;
            }

            return languages;
        }
        public async Task<List<string>> FetchFinishesForCardAsync(string uuid, SQLiteConnection conn)
        {


            var finishes = new List<string>();
            string query = @"
                SELECT finishes FROM cards WHERE uuid = @uuid 
                UNION 
                SELECT finishes FROM tokens WHERE uuid = @uuid";
            try
            {
                using var command = new SQLiteCommand(query, conn);
                command.Parameters.AddWithValue("@uuid", uuid);
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var finish = reader["finishes"]?.ToString();
                    if (!string.IsNullOrEmpty(finish))
                    {
                        finishes.AddRange(finish.Split(',').Select(f => f.Trim()));
                    }
                }
                // Remove unwanted finish types and resolve conflicts
                finishes = [.. finishes.Distinct().Where(f => !f.Equals("signed", StringComparison.OrdinalIgnoreCase))];

                if (finishes.Contains("foil", StringComparer.OrdinalIgnoreCase) && finishes.Contains("etched", StringComparer.OrdinalIgnoreCase))
                {
                    finishes = [.. finishes.Where(f => !f.Equals("foil", StringComparison.OrdinalIgnoreCase))];
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in FetchFinishesForCardAsync: {ex.Message}");
                throw;
            }

            return finishes;
        }

        // CRUD
        public async Task<int> AddCardAndReturnIdAsync(string uuid, string condition, string language, string finish, int cardsOwned, int cardsForTrade, SQLiteConnection conn)
        {
            const string insertSql = @"
                INSERT INTO myCollection
                    (uuid, cardsOwned, cardsForTrade, condition, language, finish)
                VALUES
                    (@uuid, @cardsOwned, @cardsForTrade, @condition, @language, @finish);
            ";

            try
            {
                using var insertCmd = new SQLiteCommand(insertSql, conn);
                insertCmd.Parameters.AddWithValue("@uuid", uuid);
                insertCmd.Parameters.AddWithValue("@cardsOwned", cardsOwned);
                insertCmd.Parameters.AddWithValue("@cardsForTrade", cardsForTrade);
                insertCmd.Parameters.AddWithValue("@condition", condition);
                insertCmd.Parameters.AddWithValue("@language", language);
                insertCmd.Parameters.AddWithValue("@finish", finish);

                await insertCmd.ExecuteNonQueryAsync();

                using var idCmd = new SQLiteCommand("SELECT last_insert_rowid();", conn);
                var result = await idCmd.ExecuteScalarAsync();

                return Convert.ToInt32(result);
            }
            catch (SQLiteException ex) when (ex.ResultCode == SQLiteErrorCode.Constraint)
            {
                throw new InvalidOperationException(
                    $"Duplicate CollectionIdentity detected. " +
                    $"Uuid={uuid}, Language={language}, Finish={finish}, Condition={condition}.",
                    ex);
            }
        }
        public async Task DeleteCardByIdAsync(int cardId, SQLiteConnection conn)
        {
            const string sql = @"DELETE FROM myCollection WHERE id = @id;";

            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", cardId);

            await cmd.ExecuteNonQueryAsync();
        }
        public async Task UpdateCardFieldsByIdAsync(int id, int owned, int trade, string condition, string language, string finish, SQLiteConnection conn)
        {
            const string sql = @"
                UPDATE myCollection
                   SET cardsOwned    = @owned,
                       cardsForTrade = @trade,
                       condition     = @cond,
                       language      = @lang,
                       finish        = @fin
                 WHERE id = @id;
            ";

            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@owned", owned);
            cmd.Parameters.AddWithValue("@trade", trade);
            cmd.Parameters.AddWithValue("@cond", condition);
            cmd.Parameters.AddWithValue("@lang", language);
            cmd.Parameters.AddWithValue("@fin", finish);
            cmd.Parameters.AddWithValue("@id", id);

            await cmd.ExecuteNonQueryAsync();
        }

    }
}
