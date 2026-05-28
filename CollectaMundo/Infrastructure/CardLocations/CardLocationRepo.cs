using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.DomainLogic.Shared.Models;
using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.CardLocations
{
    public sealed class CardLocationRepo : ICardLocationRepo
    {
        // CREATE
        public async Task<int> InsertAsync(SQLiteConnection conn, SQLiteTransaction tx, string name, string type)
        {
            const string sql = """
                INSERT INTO cardLocations (name, type)
                VALUES (@name, @type);

                SELECT last_insert_rowid();
                """;

            using var cmd = new SQLiteCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@type", type);

            object? scalar = await cmd.ExecuteScalarAsync();

            if (scalar is null || scalar == DBNull.Value)
            {
                throw new InvalidOperationException("InsertAsync failed to return a new card location id.");
            }

            return Convert.ToInt32(scalar);
        }
        public async Task<IReadOnlyList<CardLocationRecord>> InsertManyAsync(SQLiteConnection conn, SQLiteTransaction tx, IReadOnlyList<(string Name, string Type)> locations, CancellationToken token)
        {
            const string sql = """
                                INSERT INTO cardLocations (name, type)
                                VALUES (@name, @type);

                                SELECT last_insert_rowid();
                                """;

            var created = new List<CardLocationRecord>(locations.Count);

            using var cmd = new SQLiteCommand(sql, conn, tx);
            cmd.Parameters.Add("@name", System.Data.DbType.String);
            cmd.Parameters.Add("@type", System.Data.DbType.String);

            foreach (var location in locations)
            {
                token.ThrowIfCancellationRequested();

                cmd.Parameters["@name"].Value = location.Name;
                cmd.Parameters["@type"].Value = location.Type;

                object? scalar = await cmd.ExecuteScalarAsync(token);

                if (scalar is null || scalar == DBNull.Value)
                {
                    throw new InvalidOperationException(
                        $"InsertManyAsync failed to return a new card location id for '{location.Name}'.");
                }

                created.Add(new CardLocationRecord
                {
                    Id = Convert.ToInt32(scalar),
                    Name = location.Name,
                    Type = location.Type
                });
            }

            return created;
        }
        public async Task UpsertMetadataAsync(SQLiteConnection conn, SQLiteTransaction tx, int locationId, string? format, string? description)
        {
            const string sql = """
                                INSERT INTO myDecks (locationId, format, description)
                                VALUES (@locationId, @format, @description)
                                ON CONFLICT(locationId) DO UPDATE SET
                                    format = excluded.format,
                                    description = excluded.description;
                                """;

            using var cmd = new SQLiteCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("@locationId", locationId);
            cmd.Parameters.AddWithValue("@format", format ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@description", description ?? (object)DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
        }

        // READ
        public async Task<IReadOnlyList<CardLocationRecord>> GetAllLocationsAsync(SQLiteConnection conn)
        {
            const string sql = """
                SELECT id, name, type
                FROM cardLocations
                ORDER BY type ASC, name COLLATE NOCASE ASC;
                """;

            var results = new List<CardLocationRecord>();

            using var cmd = new SQLiteCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                results.Add(new CardLocationRecord
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    Name = reader.GetString(reader.GetOrdinal("name")),
                    Type = reader.GetString(reader.GetOrdinal("type"))
                });
            }

            return results;
        }
        public async Task<IReadOnlyList<DeckManagementRecord>> GetAllDecksAsync(SQLiteConnection conn)
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
        public async Task<IReadOnlyList<MyCollectionRow>> GetAllCollectionRowsAsync(SQLiteConnection conn, SQLiteTransaction tx)
        {
            const string sql = """
                                SELECT id, uuid, language, finish, condition, locationId, comment, cardsOwned, cardsForTrade
                                FROM myCollection;
                                """;

            var rows = new List<MyCollectionRow>();

            using var cmd = new SQLiteCommand(sql, conn, tx);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var id = reader.GetInt32(reader.GetOrdinal("id"));
                var uuid = reader.GetString(reader.GetOrdinal("uuid"));
                var language = reader.GetString(reader.GetOrdinal("language"));
                var finish = reader.GetString(reader.GetOrdinal("finish"));
                var condition = reader.GetString(reader.GetOrdinal("condition"));
                var locationIdOrdinal = reader.GetOrdinal("locationId");
                var commentOrdinal = reader.GetOrdinal("comment");

                int? locationId = reader.IsDBNull(locationIdOrdinal)
                    ? null
                    : reader.GetInt32(locationIdOrdinal);

                string? comment = reader.IsDBNull(commentOrdinal)
                    ? null
                    : reader.GetString(commentOrdinal);

                rows.Add(new MyCollectionRow
                {
                    CardId = id,
                    Identity = CollectionIdentityFactory.Create(
                        uuid,
                        condition,
                        language,
                        finish,
                        locationId,
                        comment),
                    CardsOwned = reader.GetInt32(reader.GetOrdinal("cardsOwned")),
                    CardsForTrade = reader.GetInt32(reader.GetOrdinal("cardsForTrade"))
                });
            }

            return rows;
        }
        public async Task<IReadOnlyList<MyCollectionRow>> GetCollectionRowsByLocationIdAsync(SQLiteConnection conn, SQLiteTransaction tx, int locationId)
        {
            const string sql = """
                                SELECT id, uuid, language, finish, condition, locationId, comment, cardsOwned, cardsForTrade
                                FROM myCollection
                                WHERE locationId = @locationId;
                                """;

            var rows = new List<MyCollectionRow>();

            using var cmd = new SQLiteCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("@locationId", locationId);

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var id = reader.GetInt32(reader.GetOrdinal("id"));
                var uuid = reader.GetString(reader.GetOrdinal("uuid"));
                var language = reader.GetString(reader.GetOrdinal("language"));
                var finish = reader.GetString(reader.GetOrdinal("finish"));
                var condition = reader.GetString(reader.GetOrdinal("condition"));
                var commentOrdinal = reader.GetOrdinal("comment");

                string? comment = reader.IsDBNull(commentOrdinal)
                    ? null
                    : reader.GetString(commentOrdinal);

                rows.Add(new MyCollectionRow
                {
                    CardId = id,
                    Identity = CollectionIdentityFactory.Create(
                        uuid,
                        condition,
                        language,
                        finish,
                        locationId,
                        comment),
                    CardsOwned = reader.GetInt32(reader.GetOrdinal("cardsOwned")),
                    CardsForTrade = reader.GetInt32(reader.GetOrdinal("cardsForTrade"))
                });
            }

            return rows;
        }
        public async Task<bool> ExistsByNameAsync(SQLiteConnection conn, SQLiteTransaction tx, string name, int? excludingId = null)
        {
            const string sql = """
                SELECT 1
                FROM cardLocations
                WHERE name = @name COLLATE NOCASE
                  AND (@excludingId IS NULL OR id <> @excludingId)
                LIMIT 1;
                """;

            using var cmd = new SQLiteCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("@name", name);

            var excludingIdParam = cmd.Parameters.AddWithValue("@excludingId", excludingId ?? (object)DBNull.Value);
            excludingIdParam.Value = excludingId.HasValue ? excludingId.Value : DBNull.Value;

            object? scalar = await cmd.ExecuteScalarAsync();
            return scalar is not null && scalar != DBNull.Value;
        }

        // UPDATE
        public async Task<int> UpdateAsync(SQLiteConnection conn, SQLiteTransaction tx, int id, string name, string type)
        {
            const string sql = """
                UPDATE cardLocations
                SET name = @name,
                    type = @type
                WHERE id = @id;
                """;

            using var cmd = new SQLiteCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@type", type);

            return await cmd.ExecuteNonQueryAsync();
        }

        // DELETE
        public async Task<int> DeleteDeckMetadataAsync(SQLiteConnection conn, SQLiteTransaction tx, int locationId)
        {
            const string sql = """
                                DELETE FROM myDecks
                                WHERE locationId = @locationId;
                                """;

            using var cmd = new SQLiteCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("@locationId", locationId);

            return await cmd.ExecuteNonQueryAsync();
        }
        public async Task<int> DeleteAsync(SQLiteConnection conn, SQLiteTransaction tx, int id)
        {
            const string sql = """
                DELETE FROM cardLocations
                WHERE id = @id;
                """;

            using var cmd = new SQLiteCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("@id", id);

            return await cmd.ExecuteNonQueryAsync();
        }
    }
}
