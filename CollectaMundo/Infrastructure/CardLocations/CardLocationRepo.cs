using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.DomainLogic.Shared.Models;
using CollectaMundo.Infrastructure.Shared;
using System.Data.Common;
using System.Data.SQLite;


namespace CollectaMundo.Infrastructure.CardLocations
{
    public sealed class CardLocationRepo : ICardLocationRepo
    {
        // CREATE
        public async Task<int> InsertAsync(SQLiteConnection conn, SQLiteTransaction tx, string name, string type)
        {
            var created = await InsertManyAsync(conn, tx, [(name, type)], CancellationToken.None);
            return created[0].Id;
        }
        public async Task<IReadOnlyList<CardLocationRecord>> InsertManyAsync(SQLiteConnection conn, SQLiteTransaction tx, IReadOnlyList<(string Name, string Type)> locations, CancellationToken token)
        {
            const string sql = """
                                INSERT INTO cardLocations (name, type)
                                VALUES (@name, @type);

                                SELECT last_insert_rowid();
                                """;

            var created = new List<CardLocationRecord>(locations.Count);

            using var cmd = DbHelpers.CreateCommand(conn, tx, sql);
            DbHelpers.AddString(cmd, "@name", "");
            DbHelpers.AddString(cmd, "@type", "");

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

            using var cmd = DbHelpers.CreateCommand(conn, tx, sql);
            DbHelpers.AddInt32(cmd, "@locationId", locationId);
            DbHelpers.AddNullableString(cmd, "@format", format);
            DbHelpers.AddNullableString(cmd, "@description", description);

            await cmd.ExecuteNonQueryAsync();
        }

        // READ
        public async Task<IReadOnlyList<CardLocationRecord>> GetAllLocationsAsync(SQLiteConnection conn, SQLiteTransaction? tx = null)
        {
            const string sql = """
                                SELECT id, name, type
                                FROM cardLocations
                                ORDER BY name
                                """;

            using var cmd = DbHelpers.CreateCommand(conn, tx, sql);

            var results = new List<CardLocationRecord>();

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                results.Add(new CardLocationRecord
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Type = reader.GetString(2)
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
        public Task<IReadOnlyList<MyCollectionRow>> GetAllCollectionRowsAsync(SQLiteConnection conn, SQLiteTransaction tx)
        {
            const string sql = """
                                SELECT id, uuid, language, finish, condition,
                                       locationId, comment, cardsOwned, cardsForTrade
                                FROM myCollection;
                                """;

            return ExecuteCollectionRowQueryAsync(conn, tx, sql);
        }
        public Task<IReadOnlyList<MyCollectionRow>> GetCollectionRowsByLocationIdAsync(SQLiteConnection conn, SQLiteTransaction tx, int locationId)
        {
            const string sql = """
                            SELECT id, uuid, language, finish, condition,
                                   locationId, comment, cardsOwned, cardsForTrade
                            FROM myCollection
                            WHERE locationId = @locationId;
                            """;

            return ExecuteCollectionRowQueryAsync(conn, tx, sql, cmd => cmd.Parameters.AddWithValue("@locationId", locationId));
        }
        public async Task<bool> ExistsByIdAsync(SQLiteConnection conn, SQLiteTransaction tx, int id)
        {
            const string sql = """
                                SELECT 1
                                FROM cardLocations
                                WHERE id = @id
                                LIMIT 1;
                                """;

            using var cmd = DbHelpers.CreateCommand(conn, tx, sql);
            DbHelpers.AddInt32(cmd, "@id", id);

            return await DbHelpers.ExistsAsync(cmd);
        }
        private static async Task<IReadOnlyList<MyCollectionRow>> ExecuteCollectionRowQueryAsync(SQLiteConnection conn, SQLiteTransaction tx, string sql, Action<SQLiteCommand>? configureCommand = null)
        {
            var rows = new List<MyCollectionRow>();

            using var cmd = DbHelpers.CreateCommand(conn, tx, sql);

            configureCommand?.Invoke(cmd);

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                rows.Add(MapCollectionRow(reader));
            }

            return rows;
        }
        private static MyCollectionRow MapCollectionRow(DbDataReader reader)
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

            return new MyCollectionRow
            {
                CardId = id,
                Identity = CollectionIdentityFactory.Create(uuid, condition, language, finish, locationId, comment),
                CardsOwned = reader.GetInt32(reader.GetOrdinal("cardsOwned")),
                CardsForTrade = reader.GetInt32(reader.GetOrdinal("cardsForTrade"))
            };
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

            using var cmd = DbHelpers.CreateCommand(conn, tx, sql);
            DbHelpers.AddString(cmd, "@name", name);
            DbHelpers.AddNullableInt32(cmd, "@excludingId", excludingId);

            return await DbHelpers.ExistsAsync(cmd);
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

            using var cmd = DbHelpers.CreateCommand(conn, tx, sql);
            DbHelpers.AddInt32(cmd, "@id", id);
            DbHelpers.AddString(cmd, "@name", name);
            DbHelpers.AddString(cmd, "@type", type);

            return await cmd.ExecuteNonQueryAsync();
        }

        // DELETE
        public async Task<int> DeleteDeckMetadataAsync(SQLiteConnection conn, SQLiteTransaction tx, int locationId)
        {
            const string sql = """
                                DELETE FROM myDecks
                                WHERE locationId = @locationId;
                                """;

            using var cmd = DbHelpers.CreateCommand(conn, tx, sql);
            DbHelpers.AddInt32(cmd, "@locationId", locationId);

            return await cmd.ExecuteNonQueryAsync();
        }
        public async Task<int> DeleteAsync(SQLiteConnection conn, SQLiteTransaction tx, int id)
        {
            const string sql = """
                DELETE FROM cardLocations
                WHERE id = @id;
                """;

            using var cmd = DbHelpers.CreateCommand(conn, tx, sql);
            DbHelpers.AddInt32(cmd, "@id", id);

            return await cmd.ExecuteNonQueryAsync();
        }
    }
}
