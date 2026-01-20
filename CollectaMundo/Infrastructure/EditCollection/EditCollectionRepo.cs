using CollectaMundo.DomainLogic.CardLists.Models;
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
        public async Task<int?> FindCardIdByCollectionIdentityAsync(string uuid, string condition, string language, string finish, SQLiteConnection conn)
        {
            const string sql =
            @"
                SELECT id
                FROM myCollection
                WHERE uuid = @uuid
                  AND condition = @condition
                  AND language = @language
                  AND finish = @finish
                LIMIT 1;
            ";

            using var cmd = new SQLiteCommand(sql, conn);

            cmd.Parameters.AddWithValue("@uuid", uuid);
            cmd.Parameters.AddWithValue("@condition", condition);
            cmd.Parameters.AddWithValue("@language", language);
            cmd.Parameters.AddWithValue("@finish", finish);

            var result = await cmd.ExecuteScalarAsync();

            return result is null || result is DBNull
                ? null
                : Convert.ToInt32(result);
        }


        // CRUD
        public async Task<int> AddCardAndReturnIdAsync(CardSet card, SQLiteConnection conn)
        {
            const string insertSql = @"
                INSERT INTO myCollection (uuid, cardsOwned, cardsForTrade, condition, language, finish)
                VALUES (@uuid, @cardsOwned, @cardsForTrade, @condition, @language, @finish)";
            try
            {



                // 1) Perform the insert
                using var insertCmd = new SQLiteCommand(insertSql, conn);
                insertCmd.Parameters.AddWithValue("@uuid", card.Uuid);
                insertCmd.Parameters.AddWithValue("@cardsOwned", card.CardsOwned);
                insertCmd.Parameters.AddWithValue("@cardsForTrade", card.CardsForTrade);
                insertCmd.Parameters.AddWithValue("@condition", card.SelectedCondition);
                insertCmd.Parameters.AddWithValue("@language", card.Language);
                insertCmd.Parameters.AddWithValue("@finish", card.SelectedFinish);

                await insertCmd.ExecuteNonQueryAsync();



                // 2) Retrieve the newly-generated rowid
                using var idCmd = new SQLiteCommand("SELECT last_insert_rowid()", conn);
                var result = await idCmd.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in AddCardAndReturnIdAsync: {ex}");
                throw;
            }
        }
        public async Task DeleteCardByIdAsync(CardSet card, SQLiteConnection conn)
        {
            string deleteSql = "DELETE FROM myCollection WHERE id = @id";
            try
            {
                using var deleteCommand = new SQLiteCommand(deleteSql, conn);
                deleteCommand.Parameters.AddWithValue("@id", card.CardId);
                await deleteCommand.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in DeleteCardByIdAsync: {ex.Message}");
                throw;
            }
        }
        public async Task DeleteCardsByIdsAsync(IEnumerable<int> ids, SQLiteConnection conn)
        {
            var idList = ids?.ToList() ?? [];
            if (idList.Count == 0)
            {
                return;
            }

            var paramNames = idList.Select((_, i) => $"@id{i}").ToArray();
            var sql = $"DELETE FROM myCollection WHERE id IN ({string.Join(",", paramNames)});";

            using var cmd = new SQLiteCommand(sql, conn);
            for (int i = 0; i < idList.Count; i++)
            {
                cmd.Parameters.AddWithValue(paramNames[i], idList[i]);
            }

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
