using CollectaMundo.Models;
using System.Data.SQLite;
using System.Diagnostics;

namespace CollectaMundo.Data
{
    public class SqliteCardRepository : ICardRepository
    {
        public async Task<int?> CheckForExistingCardAsync(CardSet card)
        {
            string selectSql = @"
                SELECT id FROM myCollection 
                WHERE uuid = @uuid 
                  AND condition = @condition 
                  AND language = @language 
                  AND finish = @finish";
            try
            {
                using var selectCommand = new SQLiteCommand(selectSql, DBAccess.connection);
                selectCommand.Parameters.AddWithValue("@uuid", card.Uuid);
                selectCommand.Parameters.AddWithValue("@condition", card.SelectedCondition);
                selectCommand.Parameters.AddWithValue("@language", card.Language);
                selectCommand.Parameters.AddWithValue("@finish", card.SelectedFinish);

                using var reader = await selectCommand.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return reader.GetInt32(0); // Return the id if found
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in CheckForExistingCardAsync: {ex.Message}");
                throw;
            }
            return null;
        }
        public async Task AddCardAsync(CardSet card)
        {
            string insertSql = @"
                INSERT INTO myCollection (uuid, count, trade, condition, language, finish)
                VALUES (@uuid, @count, @trade, @condition, @language, @finish)";
            try
            {
                using var insertCommand = new SQLiteCommand(insertSql, DBAccess.connection);
                insertCommand.Parameters.AddWithValue("@uuid", card.Uuid);
                insertCommand.Parameters.AddWithValue("@count", card.CardsOwned);
                insertCommand.Parameters.AddWithValue("@trade", card.CardsForTrade);
                insertCommand.Parameters.AddWithValue("@condition", card.SelectedCondition);
                insertCommand.Parameters.AddWithValue("@language", card.Language);
                insertCommand.Parameters.AddWithValue("@finish", card.SelectedFinish);

                await insertCommand.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in AddCardAsync: {ex.Message}");
                throw;
            }
        }
        public async Task UpdateCardAsync(CardSet card)
        {
            // We treat card.CardsOwned and card.CardsForTrade as the *increment* amounts.
            string updateSql = @"
                UPDATE myCollection 
                SET 
                  count     = count + @addCount,
                  trade     = trade + @addTrade,
                  condition = @condition,
                  language  = @language,
                  finish    = @finish 
                WHERE id = @cardId";
            try
            {
                using var cmd = new SQLiteCommand(updateSql, DBAccess.connection);
                cmd.Parameters.AddWithValue("@addCount", card.CardsOwned);
                cmd.Parameters.AddWithValue("@addTrade", card.CardsForTrade);
                cmd.Parameters.AddWithValue("@condition", card.SelectedCondition);
                cmd.Parameters.AddWithValue("@language", card.Language);
                cmd.Parameters.AddWithValue("@finish", card.SelectedFinish);
                cmd.Parameters.AddWithValue("@cardId", card.CardId);

                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in UpdateCardAsync: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteCardAsync(CardSet card)
        {
            string deleteSql = "DELETE FROM myCollection WHERE uuid = @uuid";
            try
            {
                using var deleteCommand = new SQLiteCommand(deleteSql, DBAccess.connection);
                deleteCommand.Parameters.AddWithValue("@uuid", card.Uuid);
                await deleteCommand.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in DeleteCardAsync: {ex.Message}");
                throw;
            }
        }
        public async Task<List<string>> FetchLanguagesForCardAsync(string uuid)
        {
            if (string.IsNullOrEmpty(uuid))
            {
                return new List<string> { "English" };
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
                using var command = new SQLiteCommand(query, DBAccess.connection);
                command.Parameters.AddWithValue("@uuid", uuid);
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string language = reader["language"] as string;
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
        public async Task<List<string>> FetchFinishesForCardAsync(string uuid)
        {
            var finishes = new List<string>();
            string query = @"
                SELECT finishes FROM cards WHERE uuid = @uuid 
                UNION 
                SELECT finishes FROM tokens WHERE uuid = @uuid";
            try
            {
                using var command = new SQLiteCommand(query, DBAccess.connection);
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
                finishes = finishes.Distinct()
                                   .Where(f => !f.Equals("signed", StringComparison.OrdinalIgnoreCase))
                                   .ToList();

                if (finishes.Contains("foil", StringComparer.OrdinalIgnoreCase) &&
                    finishes.Contains("etched", StringComparer.OrdinalIgnoreCase))
                {
                    finishes = finishes.Where(f => !f.Equals("foil", StringComparison.OrdinalIgnoreCase)).ToList();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in FetchFinishesForCardAsync: {ex.Message}");
                throw;
            }
            return finishes;
        }
    }
}
