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
    }
}
