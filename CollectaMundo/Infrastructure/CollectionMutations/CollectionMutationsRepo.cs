using CollectaMundo.Infrastructure.Shared;
using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.CollectionMutations
{
    public class CollectionMutationsRepo : ICollectionMutationsRepo
    {
        public async Task<int> AddCardAndReturnIdAsync(string uuid, string condition, string language, string finish, int? locationId, string? comment, int cardsOwned, int cardsForTrade, SQLiteConnection conn)
        {
            const string insertSql = """
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
                insertCmd.Parameters.AddWithValue("@locationId", DbHelpers.ToDbNullableInt(locationId));
                insertCmd.Parameters.AddWithValue("@comment", DbHelpers.ToDbNullableString(comment));

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
                    $"LocationId={(locationId?.ToString() ?? "null")}, Comment={(DbHelpers.NormalizeNullableString(comment) ?? "null")}.",
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
        public async Task UpdateCardFieldsByIdAsync(int id, int owned, int trade, string condition, string language, string finish, int? locationId, string? comment, SQLiteConnection conn)
        {
            const string sql = """
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
            cmd.Parameters.AddWithValue("@locationId", DbHelpers.ToDbNullableInt(locationId));
            cmd.Parameters.AddWithValue("@comment", DbHelpers.ToDbNullableString(comment));
            cmd.Parameters.AddWithValue("@id", id);

            await cmd.ExecuteNonQueryAsync();
        }
    }
}
