using CollectaMundo.DomainLogic;
using CollectaMundo.DomainLogic.Models;
using System.Data.SQLite;
using System.Diagnostics;

namespace CollectaMundo.Data
{
    public class EditCollectionRepository : IEditCollectionRepository
    {
        public async Task<int?> CheckForExistingCardAsync(CardSet card)
        {
            await DBAccess.OpenConnectionAsync();

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
                Debug.WriteLine($"Error in CheckForExistingCardAsync: {ex.Message}");
                throw;
            }
            finally
            {
                DBAccess.CloseConnection();
            }
            return null;
        }
        public async Task AddCardAsync(CardSet card)
        {
            string insertSql = @"
                INSERT INTO myCollection (uuid, cardsOwned, cardsForTrade, condition, language, finish)
                VALUES (@uuid, @cardsOwned, @cardsForTrade, @condition, @language, @finish)";
            try
            {
                await DBAccess.OpenConnectionAsync();

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
            finally
            {
                DBAccess.CloseConnection();
            }
        }
        public async Task EditCardAsync(CardSet card)
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
                await DBAccess.OpenConnectionAsync();

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
            finally
            {
                DBAccess.CloseConnection();
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
                await DBAccess.OpenConnectionAsync();

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
            finally
            {
                DBAccess.CloseConnection();
            }
        }
        public async Task DeleteCardAsync(CardSet card)
        {
            await DBAccess.OpenConnectionAsync();

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
            finally
            {
                DBAccess.CloseConnection();
            }
        }
        public async Task MergeDuplicateRecordsAsync(string uuid, string condition, string language, string finish)
        {
            const string selectSql = @"
                SELECT id, cardsOwned, cardsForTrade
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

            await DBAccess.OpenConnectionAsync();
            try
            {
                // 1) pull all matching rows
                var rows = new List<(int id, int owned, int trade)>();
                using (var cmd = new SQLiteCommand(selectSql, DBAccess.connection))
                {
                    cmd.Parameters.AddWithValue("@uuid", uuid);
                    cmd.Parameters.AddWithValue("@cond", condition);
                    cmd.Parameters.AddWithValue("@lang", language);
                    cmd.Parameters.AddWithValue("@fin", finish);

                    using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        rows.Add((
                            rdr.GetInt32(0),
                            rdr.GetInt32(1),
                            rdr.GetInt32(2)));
                    }
                }

                if (rows.Count <= 1)
                    return; // nothing to merge

                // 2) sum
                var sumOwned = rows.Sum(r => r.owned);
                var sumTrade = rows.Sum(r => r.trade);
                var keepId = rows[0].id;  // choose the first as survivor

                // 3) update survivor
                using (var upd = new SQLiteCommand(updateSql, DBAccess.connection))
                {
                    upd.Parameters.AddWithValue("@sumOwned", sumOwned);
                    upd.Parameters.AddWithValue("@sumTrade", sumTrade);
                    upd.Parameters.AddWithValue("@keepId", keepId);
                    await upd.ExecuteNonQueryAsync();
                }

                // 4) delete the rest
                using (var del = new SQLiteCommand(deleteSql, DBAccess.connection))
                {
                    del.Parameters.AddWithValue("@uuid", uuid);
                    del.Parameters.AddWithValue("@cond", condition);
                    del.Parameters.AddWithValue("@lang", language);
                    del.Parameters.AddWithValue("@fin", finish);
                    del.Parameters.AddWithValue("@keepId", keepId);
                    await del.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in MergeDuplicateRecordsAsync: {ex.Message}");
                throw;
            }
            finally
            {
                DBAccess.CloseConnection();
            }
        }
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
            try
            {
                await DBAccess.OpenConnectionAsync();

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
            try
            {
                await DBAccess.OpenConnectionAsync();

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
        public async Task<CardSet> GetMyCollectionRecordAsync(string uuid, string condition, string language, string finish)
        {
            const string sql = @"
              SELECT * FROM view_myCollection
               WHERE uuid=@uuid AND condition=@cond
                 AND language=@lang AND finish=@fin
              LIMIT 1";
            CardSet card;

            try
            {
                await DBAccess.OpenConnectionAsync();

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
                Debug.WriteLine($"Error in GetMyCollectionRecordAsync: {ex}");
                throw;
            }
            finally
            {
                DBAccess.CloseConnection();
            }
            return card;
        }

    }
}
