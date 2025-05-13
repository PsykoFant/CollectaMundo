using CollectaMundo.DomainLogic;
using CollectaMundo.DomainLogic.Models;
using System.Data.SQLite;
using System.Diagnostics;

namespace CollectaMundo.Data
{
    public class EditCollectionRepository : IEditCollectionRepository
    {
        // Lookups

        public async Task<List<string>> FetchLanguagesForCardAsync(string uuid)
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

            await DBAccess.OpenConnectionAsync();
            try
            {
                using var command = new SQLiteCommand(query, DBAccess.connection);
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
            finally
            {
                DBAccess.CloseConnection();
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

            await DBAccess.OpenConnectionAsync();
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
            finally
            {
                DBAccess.CloseConnection();
            }

            return finishes;
        }


        public async Task<int?> FindExistingCardReturnIdAsync(CardSet card)
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
                    Debug.WriteLine($"Fandt et eksisterende kort i samlingen!");
                    return reader.GetInt32(0); // Return the id if found
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in FindExistingCardReturnIdAsync: {ex.Message}");
                throw;
            }
            return null;
        }
        public async Task<CardSet> FindExistingCardReturnRecordAsync(string uuid, string condition, string language, string finish)
        {
            const string sql = @"
              SELECT * FROM view_myCollection
               WHERE uuid=@uuid AND condition=@cond
                 AND language=@lang AND finish=@fin
              LIMIT 1";
            CardSet card;

            try
            {
                using var cmd = new SQLiteCommand(sql, DBAccess.connection);
                cmd.Parameters.AddWithValue("@uuid", uuid);
                cmd.Parameters.AddWithValue("@cond", condition);
                cmd.Parameters.AddWithValue("@lang", language);
                cmd.Parameters.AddWithValue("@fin", finish);

                using var rdr = await cmd.ExecuteReaderAsync();
                if (!await rdr.ReadAsync())
                {
                    throw new InvalidOperationException("Card not found after upsert.");
                }

                // map into your local variable:
                card = CardFactory.FromMyCollectionRow(rdr);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in FindExistingCardReturnRecordAsync: {ex}");
                throw;
            }

            return card;
        }
        public async Task<List<int>> FindRecordByIdAsync(string uuid, string condition, string language, string finish)
        {
            const string sql = @"
                SELECT id
                  FROM myCollection
                 WHERE uuid      = @uuid
                   AND condition = @cond
                   AND language  = @lang
                   AND finish    = @fin;
            ";

            var ids = new List<int>();
            try
            {
                using var cmd = new SQLiteCommand(sql, DBAccess.connection);
                cmd.Parameters.AddWithValue("@uuid", uuid);
                cmd.Parameters.AddWithValue("@cond", condition);
                cmd.Parameters.AddWithValue("@lang", language);
                cmd.Parameters.AddWithValue("@fin", finish);

                using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    ids.Add(rdr.GetInt32(0));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in FindRecordByIdAsync: {ex}");
                throw;
            }
            return ids;
        }


        public async Task AddCardAsync(CardSet card)
        {
            string insertSql = @"
                INSERT INTO myCollection (uuid, cardsOwned, cardsForTrade, condition, language, finish)
                VALUES (@uuid, @cardsOwned, @cardsForTrade, @condition, @language, @finish)";
            try
            {
                using var cmd = new SQLiteCommand(insertSql, DBAccess.connection);
                cmd.Parameters.AddWithValue("@uuid", card.Uuid);
                cmd.Parameters.AddWithValue("@cardsOwned", card.CardsOwned);
                cmd.Parameters.AddWithValue("@cardsForTrade", card.CardsForTrade);
                cmd.Parameters.AddWithValue("@condition", card.SelectedCondition);
                cmd.Parameters.AddWithValue("@language", card.Language);
                cmd.Parameters.AddWithValue("@finish", card.SelectedFinish);

                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in AddCardAsync: {ex.Message}");
                throw;
            }
        }
        public async Task UpdateCardAsync(CardSet card)
        {

            string updateSql = @"
                UPDATE myCollection 
                SET 
                    condition = @condition,
                    language = @language,
                    finish = @finish,
                    cardsOwned = @cardsOwned,
                    cardsForTrade = @cardsForTrade
                WHERE id = @cardId";
            try
            {
                using var cmd = new SQLiteCommand(updateSql, DBAccess.connection);
                cmd.Parameters.AddWithValue("@cardsOwned", card.CardsOwned);
                cmd.Parameters.AddWithValue("@cardsForTrade", card.CardsForTrade);
                cmd.Parameters.AddWithValue("@condition", card.SelectedCondition);
                cmd.Parameters.AddWithValue("@language", card.Language);
                cmd.Parameters.AddWithValue("@finish", card.SelectedFinish);
                cmd.Parameters.AddWithValue("@cardId", card.CardId);

                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in UpdateCardCountsAsync: {ex.Message}");
                throw;
            }
        }
        public async Task UpdateCardCountsAsync(CardSet card)
        {
            // We treat card.CardsOwned and card.CardsForTrade as the *increment* amounts.
            string updateSql = @"
                UPDATE myCollection 
                SET 
                  cardsOwned = cardsOwned + @addCount,
                  cardsForTrade = cardsForTrade + @addTrade
                WHERE id = @cardId";
            try
            {
                using var cmd = new SQLiteCommand(updateSql, DBAccess.connection);
                cmd.Parameters.AddWithValue("@addCount", card.CardsOwned);
                cmd.Parameters.AddWithValue("@addTrade", card.CardsForTrade);
                cmd.Parameters.AddWithValue("@cardId", card.CardId);

                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in UpdateCardCountsAsync: {ex.Message}");
                throw;
            }
        }
        public async Task DeleteCardByIdAsync(CardSet card)
        {
            string deleteSql = "DELETE FROM myCollection WHERE id = @id";
            try
            {
                using var deleteCommand = new SQLiteCommand(deleteSql, DBAccess.connection);
                deleteCommand.Parameters.AddWithValue("@id", card.CardId);
                await deleteCommand.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in DeleteCardByIdAsync: {ex.Message}");
                throw;
            }
        }
        public async Task MergeDuplicateRecordsAsync(string uuid, string condition, string language, string finish, int keepId)
        {
            const string sumSql = @"
                SELECT 
                    COALESCE(SUM(cardsOwned), 0)    AS TotalOwned,
                    COALESCE(SUM(cardsForTrade), 0) AS TotalTrade
                  FROM myCollection
                 WHERE uuid      = @uuid
                   AND condition = @cond
                   AND language  = @lang
                   AND finish    = @fin;
            ";
            const string updateSql = @"
                UPDATE myCollection
                   SET cardsOwned    = @sumOwned,
                       cardsForTrade = @sumTrade
                 WHERE id = @keepId;
            ";
            const string deleteSql = @"
                DELETE FROM myCollection
                 WHERE uuid      = @uuid
                   AND condition = @cond
                   AND language  = @lang
                   AND finish    = @fin
                   AND id       <> @keepId;
            ";

            try
            {
                // 1) Get the totals
                long totalOwned = 0, totalTrade = 0;
                using (var sumCmd = new SQLiteCommand(sumSql, DBAccess.connection))
                {
                    sumCmd.Parameters.AddWithValue("@uuid", uuid);
                    sumCmd.Parameters.AddWithValue("@cond", condition);
                    sumCmd.Parameters.AddWithValue("@lang", language);
                    sumCmd.Parameters.AddWithValue("@fin", finish);

                    using var rdr = await sumCmd.ExecuteReaderAsync();
                    if (await rdr.ReadAsync())
                    {
                        totalOwned = rdr.GetInt64(0);
                        totalTrade = rdr.GetInt64(1);
                    }
                }

                // 2) Update the survivor
                using (var upd = new SQLiteCommand(updateSql, DBAccess.connection))
                {
                    upd.Parameters.AddWithValue("@sumOwned", totalOwned);
                    upd.Parameters.AddWithValue("@sumTrade", totalTrade);
                    upd.Parameters.AddWithValue("@keepId", keepId);
                    await upd.ExecuteNonQueryAsync();
                }

                // 3) Delete the rest
                using var del = new SQLiteCommand(deleteSql, DBAccess.connection);
                del.Parameters.AddWithValue("@uuid", uuid);
                del.Parameters.AddWithValue("@cond", condition);
                del.Parameters.AddWithValue("@lang", language);
                del.Parameters.AddWithValue("@fin", finish);
                del.Parameters.AddWithValue("@keepId", keepId);
                await del.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in MergeDuplicateRecordsAsync: {ex}");
                throw;
            }
        }

    }
}
