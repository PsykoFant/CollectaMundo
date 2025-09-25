using System.Data.SQLite;

namespace CollectaMundo.Data.CardImages
{
    public class CardImageRepo : ICardImageRepo
    {
        public async Task<string?> GetScryfallIdByUuidAsync(string uuid, SQLiteConnection conn)
        {
            string query = "SELECT scryfallId FROM cardIdentifiers WHERE uuid = @uuid UNION ALL SELECT scryfallId FROM tokenIdentifiers WHERE uuid = @uuid";

            using var selectCommand = new SQLiteCommand(query, conn);
            selectCommand.Parameters.AddWithValue("@uuid", uuid);
            using var reader = await selectCommand.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return reader["scryfallId"].ToString();
            }
            return null;
        }

        public async Task<bool> IsMultiPartCardAsync(string uuid, SQLiteConnection conn)
        {
            string query = "SELECT side FROM cards WHERE uuid = @uuid UNION ALL SELECT side FROM tokens WHERE uuid = @uuid";
            using var command = new SQLiteCommand(query, conn);
            command.Parameters.AddWithValue("@uuid", uuid);
            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return reader["side"].ToString() == "a";
            }
            return false;
        }
    }
}
