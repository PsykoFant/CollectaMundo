using CollectaMundo.DomainLogic.Decks.Models;
using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.Decks
{
    public sealed class DeckManagementRepo : IDeckManagementRepo
    {
        public async Task<IReadOnlyList<DeckManagementRecord>> GetAllAsync(SQLiteConnection conn)
        {
            const string sql = """
                SELECT
                    cl.id AS locationId,
                    cl.name AS name,
                    md.format AS format,
                    md.description AS description
                FROM cardLocations cl
                LEFT JOIN myDecks md ON md.locationId = cl.id
                WHERE cl.type = 'Deck'
                ORDER BY cl.name COLLATE NOCASE ASC;
                """;

            var decks = new List<DeckManagementRecord>();

            using var cmd = new SQLiteCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                int formatOrdinal = reader.GetOrdinal("format");
                int descriptionOrdinal = reader.GetOrdinal("description");

                string? format = reader.IsDBNull(formatOrdinal)
                    ? null
                    : reader.GetString(formatOrdinal);

                string? description = reader.IsDBNull(descriptionOrdinal)
                    ? null
                    : reader.GetString(descriptionOrdinal);

                decks.Add(new DeckManagementRecord
                {
                    LocationId = reader.GetInt32(reader.GetOrdinal("locationId")),
                    Name = reader.GetString(reader.GetOrdinal("name")),
                    Format = format,
                    Description = description
                });
            }

            return decks;
        }
        public async Task UpsertMetadataAsync(SQLiteConnection conn, int locationId, string? format, string? description)
        {
            const string sql = """
                INSERT INTO myDecks (locationId, format, description)
                VALUES (@locationId, @format, @description)
                ON CONFLICT(locationId) DO UPDATE SET
                    format = excluded.format,
                    description = excluded.description;
                """;

            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@locationId", locationId);
            cmd.Parameters.AddWithValue("@format", format ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@description", description ?? (object)DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
        }
        public async Task<int> DeleteMetadataAsync(SQLiteConnection conn, int locationId)
        {
            const string sql = """
                DELETE FROM myDecks
                WHERE locationId = @locationId;
                """;

            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@locationId", locationId);

            return await cmd.ExecuteNonQueryAsync();
        }
    }
}
