using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.CardImages
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
        public async Task<string?> GetScryfallIdByNameAsync(string uuid, SQLiteConnection conn)
        {
            string query = "SELECT ci.scryfallId FROM cards c JOIN cardIdentifiers ci ON c.uuid = ci.uuid WHERE c.name = @name ORDER BY c.releaseDate ASC, c.setCode ASC LIMIT 1";
            using var selectCommand = new SQLiteCommand(query, conn);
            selectCommand.Parameters.AddWithValue("@name", uuid);
            using var reader = await selectCommand.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return reader["scryfallId"].ToString();
            }
            return null;
        }
        public async Task<string?> GetOtherFaceScryfallIdByUuidAsync(string uuid, SQLiteConnection conn)
        {
            string query = @"
                WITH input(uuid) AS (
                  SELECT @uuid
                ),
                face_ids(str, rest) AS (
                  SELECT
                    '', (SELECT otherFaceIds FROM cards WHERE uuid = (SELECT uuid FROM input))
                  UNION ALL
                  SELECT
                    TRIM(SUBSTR(rest, 0, INSTR(rest || ',', ','))),
                    LTRIM(SUBSTR(rest, INSTR(rest || ',', ',') + 1))
                  FROM face_ids
                  WHERE rest <> ''
                )
                SELECT ci.scryfallId
                FROM face_ids
                JOIN cards c ON c.uuid = face_ids.str
                JOIN cardIdentifiers ci ON ci.uuid = c.uuid
                WHERE c.side = 'b';";


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
