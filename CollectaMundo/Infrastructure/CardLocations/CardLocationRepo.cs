using CollectaMundo.ApplicationServices.CardLocations.Models;
using CollectaMundo.DomainLogic.Shared.Factories;
using CollectaMundo.Infrastructure.Shared;
using CollectaMundo.Infrastructure.Shared.Models;
using System.Data.Common;
using System.Data.SQLite;


namespace CollectaMundo.Infrastructure.CardLocations
{
    public sealed class CardLocationRepo : ICardLocationRepo
    {
        // CREATE
        public async Task<int> CreateLocationAsync(SQLiteConnection conn, SQLiteTransaction tx, string name, string type)
        {
            var created = await CreateLocationsAsync(conn, tx, [(name, type)], CancellationToken.None);
            return created.Single().Id;
        }
        public async Task<IReadOnlyList<CardLocationDbRow>> CreateLocationsAsync(SQLiteConnection conn, SQLiteTransaction tx, IReadOnlyList<(string Name, string Type)> locations, CancellationToken token)
        {
            const string sql = """
                                INSERT INTO cardLocations (name, type)
                                VALUES (@name, @type);

                                SELECT last_insert_rowid();
                                """;

            var created = new List<CardLocationDbRow>(locations.Count);

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
                        $"CreateLocationsAsync failed to return a new card location id for '{location.Name}'.");
                }

                created.Add(CreateCardLocationRecord(Convert.ToInt32(scalar), location.Name, location.Type));
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

        // locations
        public async Task<IReadOnlyList<CardLocationDbRow>> GetAllLocationsAsync(SQLiteConnection conn, SQLiteTransaction? tx = null)
        {
            const string sql = """
                                SELECT id, name, type
                                FROM cardLocations
                                ORDER BY name COLLATE NOCASE ASC
                                """;

            using var cmd = DbHelpers.CreateCommand(conn, tx, sql);

            var results = new List<CardLocationDbRow>();

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                results.Add(CreateCardLocationRecord(reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
            }

            return results;
        }
        public async Task<IReadOnlyList<int>> GetExistingLocationIdsAsync(SQLiteConnection conn, SQLiteTransaction tx, IReadOnlyList<int> ids, CancellationToken token = default)
        {
            var distinctIds = GetDistinctIds(ids);

            if (distinctIds.Count == 0)
            {
                return [];
            }

            var parameters = CreateParameterList(distinctIds.Count);

            string sql = $"""
                        SELECT id
                        FROM cardLocations
                        WHERE id IN ({parameters.InClause});
                        """;

            using var cmd = DbHelpers.CreateCommand(conn, tx, sql);

            for (int i = 0; i < distinctIds.Count; i++)
            {
                DbHelpers.AddInt32(cmd, parameters.Names[i], distinctIds[i]);
            }

            var existingIds = new List<int>();

            using var reader = await cmd.ExecuteReaderAsync(token);

            while (await reader.ReadAsync(token))
            {
                existingIds.Add(reader.GetInt32(0));
            }

            return existingIds;
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

        // decks
        public async Task<IReadOnlyList<DeckManagementRecord>> GetAllDecksAsync(SQLiteConnection conn, SQLiteTransaction? tx = null)
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

            using var cmd = DbHelpers.CreateCommand(conn, tx, sql);
            using var reader = await cmd.ExecuteReaderAsync();

            int formatOrdinal = reader.GetOrdinal("format");
            int descriptionOrdinal = reader.GetOrdinal("description");

            while (await reader.ReadAsync())
            {
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
        public async Task<IReadOnlyList<string>> GetDeckFormatsAsync(SQLiteConnection conn, SQLiteTransaction? tx = null)
        {
            const string sql = """
                       PRAGMA table_info(cardLegalities);
                       """;

            using var cmd = DbHelpers.CreateCommand(conn, tx, sql);

            var formats = new List<string>();

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                string columnName = reader.GetString(reader.GetOrdinal("name"));

                if (!string.Equals(columnName, "uuid", StringComparison.OrdinalIgnoreCase))
                {
                    formats.Add(columnName);
                }
            }

            return [.. formats.OrderBy(format => format, StringComparer.OrdinalIgnoreCase)];
        }

        // collection rows
        public Task<IReadOnlyList<CollectionCardDbRow>> GetAllCollectionRowsAsync(SQLiteConnection conn, SQLiteTransaction tx)
        {
            const string sql = """
                                SELECT id, uuid, language, finish, condition,
                                       locationId, comment, cardsOwned, cardsForTrade
                                FROM myCollection;
                                """;

            return ExecuteCollectionRowQueryAsync(conn, tx, sql);
        }
        public async Task<IReadOnlyList<CollectionCardDbRow>> GetCollectionRowsByLocationIdsAsync(SQLiteConnection conn, SQLiteTransaction tx, IReadOnlyList<int> locationIds, CancellationToken token = default)
        {
            var distinctIds = GetDistinctIds(locationIds);

            if (distinctIds.Count == 0)
            {
                return [];
            }

            var parameters = CreateParameterList(distinctIds.Count);

            string sql = $"""
                        SELECT id, uuid, language, finish, condition, locationId, comment, cardsOwned, cardsForTrade
                        FROM myCollection
                        WHERE locationId IN ({parameters.InClause});
                        """;

            using var cmd = DbHelpers.CreateCommand(conn, tx, sql);

            for (int i = 0; i < distinctIds.Count; i++)
            {
                DbHelpers.AddInt32(cmd, parameters.Names[i], distinctIds[i]);
            }

            return await ExecuteCollectionRowQueryAsync(conn, tx, sql, cmd =>
            {
                for (int i = 0; i < distinctIds.Count; i++)
                {
                    DbHelpers.AddInt32(cmd, parameters.Names[i], distinctIds[i]);
                }
            }, token);
        }


        private static async Task<IReadOnlyList<CollectionCardDbRow>> ExecuteCollectionRowQueryAsync(SQLiteConnection conn, SQLiteTransaction tx, string sql, Action<SQLiteCommand>? configureCommand = null, CancellationToken token = default)
        {
            var rows = new List<CollectionCardDbRow>();

            using var cmd = DbHelpers.CreateCommand(conn, tx, sql);

            configureCommand?.Invoke(cmd);

            using var reader = await cmd.ExecuteReaderAsync(token);

            while (await reader.ReadAsync(token))
            {
                rows.Add(MapCollectionRow(reader));
            }

            return rows;
        }

        // UPDATE
        public async Task<int> UpdateLocationAsync(SQLiteConnection conn, SQLiteTransaction tx, int id, string name, string type)
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
        public async Task<IReadOnlyList<CardLocationDbRow>> UpdateLocationTypesAsync(SQLiteConnection conn, SQLiteTransaction tx, IReadOnlyList<int> ids, string type, CancellationToken token = default)
        {
            var distinctIds = GetDistinctIds(ids);

            if (distinctIds.Count == 0)
            {
                return [];
            }

            var parameters = CreateParameterList(distinctIds.Count);

            string updateSql = $"""
                        UPDATE cardLocations
                        SET type = @type
                        WHERE id IN ({parameters.InClause});
                        """;

            using var updateCmd = DbHelpers.CreateCommand(conn, tx, updateSql);

            DbHelpers.AddString(updateCmd, "@type", type);

            for (int i = 0; i < distinctIds.Count; i++)
            {
                DbHelpers.AddInt32(updateCmd, parameters.Names[i], distinctIds[i]);
            }

            await updateCmd.ExecuteNonQueryAsync(token);

            string selectSql = $"""
                        SELECT id, name, type
                        FROM cardLocations
                        WHERE id IN ({parameters.InClause})
                        ORDER BY name COLLATE NOCASE ASC;
                        """;

            using var selectCmd = DbHelpers.CreateCommand(conn, tx, selectSql);

            for (int i = 0; i < distinctIds.Count; i++)
            {
                DbHelpers.AddInt32(selectCmd, parameters.Names[i], distinctIds[i]);
            }

            var results = new List<CardLocationDbRow>();

            using var reader = await selectCmd.ExecuteReaderAsync(token);

            while (await reader.ReadAsync(token))
            {
                results.Add(CreateCardLocationRecord(reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
            }

            return results;
        }
        public async Task<int> UpdateDeckFormatsAsync(SQLiteConnection conn, SQLiteTransaction tx, IReadOnlyList<int> locationIds, string format, CancellationToken token = default)
        {
            var distinctIds = GetDistinctIds(locationIds);
            if (distinctIds.Count == 0)
            {
                return 0;
            }

            var parameters = CreateParameterList(distinctIds.Count);

            string sql = $"""
                         UPDATE myDecks
                         SET format = @format
                         WHERE locationId IN ({parameters.InClause});
                         """;

            using var cmd = DbHelpers.CreateCommand(conn, tx, sql);

            DbHelpers.AddNullableString(cmd, "@format", format);

            for (int i = 0; i < distinctIds.Count; i++)
            {
                DbHelpers.AddInt32(cmd, parameters.Names[i], distinctIds[i]);
            }

            return await cmd.ExecuteNonQueryAsync(token);
        }


        // DELETE
        public async Task<int> DeleteLocationsAsync(SQLiteConnection conn, SQLiteTransaction tx, IReadOnlyList<int> ids, CancellationToken token = default)
        {
            var distinctIds = GetDistinctIds(ids);

            if (distinctIds.Count == 0)
            {
                return 0;
            }

            var parameters = CreateParameterList(distinctIds.Count);

            string sql = $"""
                            DELETE FROM cardLocations
                            WHERE id IN ({parameters.InClause});
                            """;

            using var cmd = DbHelpers.CreateCommand(conn, tx, sql);

            for (int i = 0; i < distinctIds.Count; i++)
            {
                DbHelpers.AddInt32(cmd, parameters.Names[i], distinctIds[i]);
            }

            return await cmd.ExecuteNonQueryAsync(token);
        }
        public async Task<int> DeleteDecksMetadataAsync(SQLiteConnection conn, SQLiteTransaction tx, IReadOnlyList<int> locationIds, CancellationToken token = default)
        {
            var distinctIds = GetDistinctIds(locationIds);

            if (distinctIds.Count == 0)
            {
                return 0;
            }

            var parameters = CreateParameterList(distinctIds.Count);

            string sql = $"""
                            DELETE FROM myDecks
                            WHERE locationId IN ({parameters.InClause});
                            """;

            using var cmd = DbHelpers.CreateCommand(conn, tx, sql);

            for (int i = 0; i < distinctIds.Count; i++)
            {
                DbHelpers.AddInt32(cmd, parameters.Names[i], distinctIds[i]);
            }

            return await cmd.ExecuteNonQueryAsync(token);
        }


        // Helpers
        private static CardLocationDbRow CreateCardLocationRecord(int locationId, string name, string type)
        {
            return new CardLocationDbRow
            {
                Id = locationId,
                Name = name,
                Type = type
            };
        }
        private static CollectionCardDbRow MapCollectionRow(DbDataReader reader)
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

            return new CollectionCardDbRow
            {
                CardId = id,
                Identity = CollectionIdentityFactory.Create(uuid, condition, language, finish, locationId, comment),
                CardsOwned = reader.GetInt32(reader.GetOrdinal("cardsOwned")),
                CardsForTrade = reader.GetInt32(reader.GetOrdinal("cardsForTrade"))
            };
        }
        private static List<int> GetDistinctIds(IEnumerable<int> ids)
        {
            return [.. ids.Distinct()];
        }
        private sealed record SqlParameterList(IReadOnlyList<string> Names, string InClause);
        private static SqlParameterList CreateParameterList(int count, string prefix = "id")
        {
            List<string> names = [.. Enumerable.Range(0, count).Select(index => $"@{prefix}{index}")];

            return new SqlParameterList(names, string.Join(", ", names));
        }
    }
}
