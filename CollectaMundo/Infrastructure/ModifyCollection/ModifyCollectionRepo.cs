using System.Data.SQLite;
using System.Diagnostics;

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
        public async Task<int> AddCardAndReturnIdAsync(string uuid,string condition,string language,string finish,int? locationId,string? comment,int cardsOwned,int cardsForTrade,SQLiteConnection conn)
        {
            const string insertSql =    """
                                        INSERT INTO myCollection
                                            (uuid, cardsOwned, cardsForTrade, condition, language, finish, locationId, comment)
                                        VALUES
                                            (@uuid, @cardsOwned, @cardsForTrade, @condition, @language, @finish, @locationId, @comment);
                                        """;

            try
            {
                using var insertCmd = new SQLiteCommand(insertSql, conn);
                insertCmd.Parameters.AddWithValue("@uuid", uuid);
                insertCmd.Parameters.AddWithValue("@cardsOwned", cardsOwned);
                insertCmd.Parameters.AddWithValue("@cardsForTrade", cardsForTrade);
                insertCmd.Parameters.AddWithValue("@condition", condition);
                insertCmd.Parameters.AddWithValue("@language", language);
                insertCmd.Parameters.AddWithValue("@finish", finish);
                insertCmd.Parameters.AddWithValue("@locationId", locationId.HasValue ? locationId.Value : DBNull.Value);
                insertCmd.Parameters.AddWithValue("@comment", string.IsNullOrWhiteSpace(comment) ? DBNull.Value : comment.Trim());

                await insertCmd.ExecuteNonQueryAsync();

                using var idCmd = new SQLiteCommand("SELECT last_insert_rowid();", conn);
                var result = await idCmd.ExecuteScalarAsync();

                return Convert.ToInt32(result);
            }
            catch (SQLiteException ex) when (ex.ResultCode == SQLiteErrorCode.Constraint)
            {
                throw new InvalidOperationException(
                    "Duplicate CollectionIdentity detected. " +
                    $"Uuid={uuid}, Language={language}, Finish={finish}, Condition={condition}, " +
                    $"LocationId={(locationId?.ToString() ?? "null")}, Comment={(string.IsNullOrWhiteSpace(comment) ? "null" : comment.Trim())}.",
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
        public async Task UpdateCardFieldsByIdAsync(int id,int owned,int trade,string condition,string language,string finish,int? locationId,string? comment,SQLiteConnection conn)
        {
            const string sql =  """
                                UPDATE myCollection
                                   SET cardsOwned    = @owned,
                                       cardsForTrade = @trade,
                                       condition     = @cond,
                                       language      = @lang,
                                       finish        = @fin,
                                       locationId    = @locationId,
                                       comment       = @comment
                                 WHERE id = @id;
                                """;

            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@owned", owned);
            cmd.Parameters.AddWithValue("@trade", trade);
            cmd.Parameters.AddWithValue("@cond", condition);
            cmd.Parameters.AddWithValue("@lang", language);
            cmd.Parameters.AddWithValue("@fin", finish);
            cmd.Parameters.AddWithValue("@locationId", locationId.HasValue ? locationId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@comment", string.IsNullOrWhiteSpace(comment) ? DBNull.Value : comment.Trim());
            cmd.Parameters.AddWithValue("@id", id);

            await cmd.ExecuteNonQueryAsync();
        }
    }
}
