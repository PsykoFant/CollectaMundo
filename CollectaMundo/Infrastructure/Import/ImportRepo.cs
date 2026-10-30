using CollectaMundo.DomainLogic.Import.Models;
using CollectaMundo.DomainLogic.Shared.Factories;
using CollectaMundo.Infrastructure.CardLists.Models;
using CollectaMundo.Infrastructure.Shared.Database;
using System.Data;
using System.Data.SQLite;
using System.Text;

namespace CollectaMundo.Infrastructure.Import
{
    public class ImportRepo() : IImportRepo
    {
        public async Task<List<string>> GetCardIdentifierColumns(SQLiteConnection conn)
        {
            var columns = new List<string>();
            const string query = "PRAGMA table_info(cardIdentifiers);";

            using var selectCommand = new SQLiteCommand(query, conn);
            using var reader = await selectCommand.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                string? columnName = reader["name"]?.ToString();
                if (!string.IsNullOrEmpty(columnName))
                {
                    columns.Add(columnName);
                }
            }

            return columns;
        }
        public async Task<Dictionary<string, List<string>>> GetCardUuidsByIdFieldAsync(SQLiteConnection conn, string identifierFieldName, IEnumerable<string> valuesEnumerable)
        {
            var values = valuesEnumerable.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.Ordinal).ToList();

            // Return empty map early if no lookup values
            if (values.Count == 0)
            {
                return [];
            }

            // Allow only letters, digits and underscore (adjust as needed).
            if (!System.Text.RegularExpressions.Regex.IsMatch(identifierFieldName, @"^[A-Za-z0-9_]+$"))
            {
                throw new ArgumentException("Invalid identifier field name.", nameof(identifierFieldName));
            }

            // Prepare result dictionary with empty lists for each lookup value
            var result = values.ToDictionary(v => v, v => new List<string>());

            // Query card and token identifiers, restricting matches to primary faces.
            var sb = new StringBuilder();
            sb.Append("SELECT ci.uuid AS uuid, ci.").Append(identifierFieldName).Append(" AS idval ")
              .Append("FROM cardIdentifiers ci ")
              .Append("INNER JOIN cards c ON ci.uuid = c.uuid ")
              .Append("WHERE (c.side IS NULL OR c.side = 'a') AND ci.")
              .Append(identifierFieldName).Append(" IN (");

            for (int i = 0; i < values.Count; i++)
            {
                sb.Append($"@v{i},");
            }
            sb.Length--; // remove last comma
            sb.Append(") ");

            // Append UNION ALL part for tokens
            sb.Append(" UNION ALL ")
              .Append("SELECT ti.uuid AS uuid, ti.").Append(identifierFieldName).Append(" AS idval ")
              .Append("FROM tokenIdentifiers ti ")
              .Append("INNER JOIN tokens t ON ti.uuid = t.uuid ")
              .Append("WHERE (t.side IS NULL OR t.side = 'a') AND ti.")
              .Append(identifierFieldName).Append(" IN (");

            for (int i = 0; i < values.Count; i++)
            {
                sb.Append($"@v{i},");
            }
            sb.Length--; // remove last comma
            sb.Append(");");

            using var cmd = new SQLiteCommand(sb.ToString(), conn);

            // Add parameters
            for (int i = 0; i < values.Count; i++)
            {
                cmd.Parameters.AddWithValue($"@v{i}", values[i]);
            }

            // Execute and populate map
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var uuid = reader["uuid"]?.ToString();
                var idval = reader["idval"]?.ToString();

                if (string.IsNullOrEmpty(uuid) || string.IsNullOrEmpty(idval))
                {
                    // skip bad rows
                    continue;
                }

                // Only add uuids for values we requested (defensive)
                if (result.TryGetValue(idval, out var list))
                {
                    list.Add(uuid);
                }
            }

            return result;
        }

        //----------------------------
        // step 3
        //----------------------------
        //  Name + SetCode Lookup

        public async Task<Dictionary<string, List<string>>> QueryByNameAndSetCodeAsync(SQLiteConnection conn, IReadOnlyList<(string Name, string SetCode)> pairs, CancellationToken token)
        {
            var result = new Dictionary<string, List<string>>();

            // Remove invalid pairs.
            var validPairs = pairs.Where(p => !string.IsNullOrWhiteSpace(p.Name) && !string.IsNullOrWhiteSpace(p.SetCode)).ToList();

            if (validPairs.Count == 0)
            {
                return result;
            }

            // Preserve only the actual name/set-code combinations requested.
            var requestedKeys = validPairs.Select(p => $"{p.Name}_{p.SetCode}".ToLowerInvariant()).ToHashSet();

            // Deduplicate independently to reduce SQL parameters.
            var names = validPairs.Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var codes = validPairs.Select(p => p.SetCode).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            using var cmd = conn.CreateCommand();

            var namePlaceholders = new List<string>();
            var codePlaceholders = new List<string>();

            for (int i = 0; i < names.Count; i++)
            {
                string parameterName = $"@name{i}";
                namePlaceholders.Add(parameterName);
                cmd.Parameters.AddWithValue(parameterName, names[i]);
            }

            for (int i = 0; i < codes.Count; i++)
            {
                string parameterName = $"@setcode{i}";
                codePlaceholders.Add(parameterName);
                cmd.Parameters.AddWithValue(parameterName, codes[i]);
            }

            string nameInClause = string.Join(",", namePlaceholders);
            string codeInClause = string.Join(",", codePlaceholders);

            cmd.CommandText = $"""
                                SELECT uuid, name, setCode
                                FROM view_cardToken
                                WHERE name COLLATE NOCASE IN ({nameInClause})
                                  AND setCode COLLATE NOCASE IN ({codeInClause})

                                UNION ALL

                                SELECT uuid, name, tokenSetCode AS setCode
                                FROM view_cardToken
                                WHERE tokenSetCode <> setCode
                                  AND name COLLATE NOCASE IN ({nameInClause})
                                  AND tokenSetCode COLLATE NOCASE IN ({codeInClause})

                                UNION ALL

                                SELECT uuid, faceName AS name, tokenSetCode AS setCode
                                FROM view_cardToken
                                WHERE faceName COLLATE NOCASE IN ({nameInClause})
                                  AND tokenSetCode COLLATE NOCASE IN ({codeInClause});
                                """;

            using var reader = await cmd.ExecuteReaderAsync(token);

            while (await reader.ReadAsync(token))
            {
                string uuid = reader["uuid"]?.ToString() ?? "";
                string name = reader["name"]?.ToString() ?? "";
                string code = reader["setCode"]?.ToString() ?? "";

                if (string.IsNullOrWhiteSpace(uuid) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code))
                {
                    continue;
                }

                string key = $"{name}_{code}".ToLowerInvariant();

                // Ignore combinations not present in the input pairs.
                if (!requestedKeys.Contains(key))
                {
                    continue;
                }

                if (!result.TryGetValue(key, out var list))
                {
                    list = [];
                    result[key] = list;
                }

                // Prevent duplicate UUIDs from overlapping matching rules.
                if (!list.Contains(uuid, StringComparer.OrdinalIgnoreCase))
                {
                    list.Add(uuid);
                }
            }

            return result;
        }

        //  Name + SetName Lookup
        public async Task<Dictionary<string, List<string>>> QueryByNameAndSetNameAsync(SQLiteConnection conn, IReadOnlyList<(string Name, string SetName)> pairs, CancellationToken token)
        {
            var result = new Dictionary<string, List<string>>();
            var validPairs = pairs.Where(p => !string.IsNullOrWhiteSpace(p.Name) && !string.IsNullOrWhiteSpace(p.SetName)).ToList();

            if (validPairs.Count == 0)
            {
                return result;
            }

            // Preserve only the actual name/set-name combinations requested.
            var requestedKeys = validPairs.Select(p => $"{p.Name}_{p.SetName}".ToLowerInvariant()).ToHashSet();

            // Deduplicate independently to reduce SQL parameters.
            var names = validPairs.Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var setNames = validPairs.Select(p => p.SetName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            using var cmd = conn.CreateCommand();

            var namePlaceholders = new List<string>();
            var setPlaceholders = new List<string>();

            for (int i = 0; i < names.Count; i++)
            {
                string parameterName = $"@name{i}";
                namePlaceholders.Add(parameterName);
                cmd.Parameters.AddWithValue(parameterName, names[i]);
            }

            for (int i = 0; i < setNames.Count; i++)
            {
                string parameterName = $"@setname{i}";
                setPlaceholders.Add(parameterName);
                cmd.Parameters.AddWithValue(parameterName, setNames[i]);
            }

            string nameInClause = string.Join(",", namePlaceholders);
            string setInClause = string.Join(",", setPlaceholders);

            cmd.CommandText = $"""
                                SELECT uuid, name, setName
                                FROM view_cardToken
                                WHERE name COLLATE NOCASE IN ({nameInClause})
                                  AND setName COLLATE NOCASE IN ({setInClause})

                                UNION ALL

                                SELECT uuid, faceName AS name, setName
                                FROM view_cardToken
                                WHERE faceName COLLATE NOCASE IN ({nameInClause})
                                  AND setName COLLATE NOCASE IN ({setInClause});
                                """;

            using var reader = await cmd.ExecuteReaderAsync(token);

            while (await reader.ReadAsync(token))
            {
                string uuid = reader["uuid"]?.ToString() ?? "";
                string name = reader["name"]?.ToString() ?? "";
                string setName = reader["setName"]?.ToString() ?? "";

                if (string.IsNullOrWhiteSpace(uuid) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(setName))
                {
                    continue;
                }

                string key = $"{name}_{setName}".ToLowerInvariant();

                // Ignore combinations not present in the input pairs.
                if (!requestedKeys.Contains(key))
                {
                    continue;
                }

                if (!result.TryGetValue(key, out var list))
                {
                    list = [];
                    result[key] = list;
                }

                // Prevent duplicate UUIDs from overlapping matching rules.
                if (!list.Contains(uuid, StringComparer.OrdinalIgnoreCase))
                {
                    list.Add(uuid);
                }
            }

            return result;
        }


        // Name-only lookup for Step 3 fallback matching.
        public async Task<Dictionary<string, List<string>>> QueryByNameOnlyAsync(SQLiteConnection conn, IReadOnlyList<string> names, CancellationToken token)
        {
            var result = new Dictionary<string, List<string>>();

            if (names == null || names.Count == 0)
            {
                return result;
            }

            // Remove invalid and duplicate names to reduce SQL parameters.
            var distinctNames = names.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            if (distinctNames.Count == 0)
            {
                return result;
            }

            using var cmd = conn.CreateCommand();

            var placeholders = new List<string>();

            for (int i = 0; i < distinctNames.Count; i++)
            {
                string parameterName = $"@name{i}";
                placeholders.Add(parameterName);
                cmd.Parameters.AddWithValue(parameterName, distinctNames[i]);
            }

            string nameInClause = string.Join(",", placeholders);

            cmd.CommandText = $"""
                                SELECT uuid, name
                                FROM view_cardToken
                                WHERE name COLLATE NOCASE IN ({nameInClause})

                                UNION ALL

                                SELECT uuid, faceName AS name
                                FROM view_cardToken
                                WHERE faceName COLLATE NOCASE IN ({nameInClause});
                                """;

            using var reader = await cmd.ExecuteReaderAsync(token);

            while (await reader.ReadAsync(token))
            {
                string uuid = reader["uuid"]?.ToString() ?? "";
                string name = reader["name"]?.ToString() ?? "";

                if (string.IsNullOrWhiteSpace(uuid) || string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                string key = name.ToLowerInvariant();

                if (!result.TryGetValue(key, out var list))
                {
                    list = [];
                    result[key] = list;
                }

                // Prevent duplicate UUIDs from overlapping matching rules.
                if (!list.Contains(uuid, StringComparer.OrdinalIgnoreCase))
                {
                    list.Add(uuid);
                }
            }

            return result;
        }


        //----------------------------
        // step 9
        //----------------------------
        public async Task<IReadOnlyDictionary<string, BaseAvailability>> FetchBaseAvailabilityAsync(IReadOnlyCollection<string> uuids, IDbConnection connection, IDbTransaction? tx, CancellationToken token)
        {
            await PrepareTempUuidsAsync(connection, tx, uuids, token);

            var conn = (SQLiteConnection)connection;
            var sqliteTx = (SQLiteTransaction?)tx;

            const string sql = """
                                SELECT p.uuid, p.language, p.finishes
                                FROM temp_import_uuids t
                                CROSS JOIN printingCards p
                                WHERE p.uuid = t.uuid;
                                """;

            var result = new Dictionary<string, BaseAvailability>(StringComparer.OrdinalIgnoreCase);

            using var cmd = new SQLiteCommand(sql, conn, sqliteTx);
            using var reader = await cmd.ExecuteReaderAsync(token);

            while (await reader.ReadAsync(token))
            {
                token.ThrowIfCancellationRequested();

                var uuid = reader["uuid"]?.ToString();
                if (string.IsNullOrWhiteSpace(uuid))
                    continue;

                var language = reader["language"]?.ToString();
                var finishes = reader["finishes"]?.ToString();

                result[uuid] = new BaseAvailability(uuid, language, finishes);
            }

            return result;
        }
        public async Task<IReadOnlyDictionary<string, HashSet<string>>> FetchForeignLanguagesAsync(IReadOnlyCollection<string> uuids, IDbConnection connection, IDbTransaction? tx, CancellationToken token)
        {
            await PrepareTempUuidsAsync(connection, tx, uuids, token);

            var conn = (SQLiteConnection)connection;
            var sqliteTx = (SQLiteTransaction?)tx;

            const string sql = """
                                SELECT f.uuid, f.language
                                FROM temp_import_uuids t
                                CROSS JOIN cardForeignData f
                                WHERE f.uuid = t.uuid;
                                """;

            var result = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            using var cmd = new SQLiteCommand(sql, conn, sqliteTx);
            using var reader = await cmd.ExecuteReaderAsync(token);

            while (await reader.ReadAsync(token))
            {
                token.ThrowIfCancellationRequested();

                var uuid = reader["uuid"]?.ToString();
                if (string.IsNullOrWhiteSpace(uuid))
                    continue;

                var language = reader["language"]?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(language))
                    continue;

                if (!result.TryGetValue(uuid, out var languages))
                {
                    languages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    result[uuid] = languages;
                }

                languages.Add(language);
            }

            return result;
        }

        // --- TEMP TABLE SETUP ---
        private static async Task PrepareTempUuidsAsync(IDbConnection c, IDbTransaction? tx, IReadOnlyCollection<string> uuids, CancellationToken token)
        {
            var conn = (SQLiteConnection)c;
            var sqliteTx = (SQLiteTransaction?)tx;

            // 1) Create temp table (connection-scoped)
            const string createSql = """
                CREATE TEMP TABLE IF NOT EXISTS temp_import_uuids (
                    uuid TEXT PRIMARY KEY
                );
                """;

            using (var cmd = new SQLiteCommand(createSql, conn, sqliteTx))
            {
                await cmd.ExecuteNonQueryAsync(token);
            }

            // 2) Clear it (we rebuild per call for simplicity + correctness)
            using (var cmd = new SQLiteCommand("DELETE FROM temp_import_uuids;", conn, sqliteTx))
            {
                await cmd.ExecuteNonQueryAsync(token);
            }

            // 3) Insert UUIDs (OR IGNORE collapses duplicates)
            const string insertSql = "INSERT OR IGNORE INTO temp_import_uuids(uuid) VALUES (@uuid);";
            using (var cmd = new SQLiteCommand(insertSql, conn, sqliteTx))
            {
                var pUuid = cmd.Parameters.Add("@uuid", DbType.String);

                foreach (var uuid in uuids)
                {
                    token.ThrowIfCancellationRequested();

                    if (string.IsNullOrWhiteSpace(uuid))
                        continue;

                    pUuid.Value = uuid;
                    await cmd.ExecuteNonQueryAsync(token);
                }
            }
        }

        //----------------------------
        // Final upsert step that inserts/updates myCollection based on the imported data, returning the resulting rows with their assigned CardIds. This is where we apply the "additive" logic for owned/trade counts.
        //----------------------------

        public async Task<IReadOnlyList<CollectionCardDbRow>> UpsertMyCollectionAsync(IReadOnlyList<CollectionUpsertItem> items, SQLiteConnection conn, SQLiteTransaction tx, IProgress<int>? percent, CancellationToken token)
        {
            const string insertSql = """
                                    INSERT INTO myCollection
                                        (uuid, language, finish, condition, locationId, comment, cardsOwned, cardsForTrade)
                                    VALUES
                                        (@uuid, @language, @finish, @condition, @locationId, @comment, @owned, @trade);
                                    """;

            const string updateSql = """
                                    UPDATE myCollection
                                       SET cardsOwned = cardsOwned + @owned,
                                           cardsForTrade = cardsForTrade + @trade
                                     WHERE uuid = @uuid
                                       AND language = @language
                                       AND finish = @finish
                                       AND condition = @condition
                                       AND COALESCE(locationId, -1) = COALESCE(@locationId, -1)
                                       AND COALESCE(comment, '') = COALESCE(@comment, '');
                                    """;

            const string selectIdSql = """
                                        SELECT id
                                        FROM myCollection
                                        WHERE uuid = @uuid
                                          AND language = @language
                                          AND finish = @finish
                                          AND condition = @condition
                                          AND COALESCE(locationId, -1) = COALESCE(@locationId, -1)
                                          AND COALESCE(comment, '') = COALESCE(@comment, '')
                                        LIMIT 1;
                                        """;

            var result = new List<CollectionCardDbRow>(items.Count);

            if (items.Count == 0)
            {
                return result;
            }

            // Create and prepare commands once for the entire batch.
            using var updateCmd = new SQLiteCommand(updateSql, conn, tx);
            AddIdentityParameters(updateCmd);
            AddQuantityParameters(updateCmd);
            updateCmd.Prepare();

            using var insertCmd = new SQLiteCommand(insertSql, conn, tx);
            AddIdentityParameters(insertCmd);
            AddQuantityParameters(insertCmd);
            insertCmd.Prepare();

            using var selectIdCmd = new SQLiteCommand(selectIdSql, conn, tx);
            AddIdentityParameters(selectIdCmd);
            selectIdCmd.Prepare();

            for (int i = 0; i < items.Count; i++)
            {
                token.ThrowIfCancellationRequested();

                var it = items[i];
                var normalizedComment = SqliteDbHelpers.NormalizeNullableString(it.Comment);

                // 1. Attempt additive update of existing collection row.
                BindIdentityParameters(updateCmd, it, normalizedComment);
                BindQuantityParameters(updateCmd, it);

                var affectedRows = await updateCmd.ExecuteNonQueryAsync(token);

                // 2. Insert only when the identity does not already exist.
                if (affectedRows == 0)
                {
                    BindIdentityParameters(insertCmd, it, normalizedComment);
                    BindQuantityParameters(insertCmd, it);

                    await insertCmd.ExecuteNonQueryAsync(token);
                }

                // 3. Resolve the database ID of the inserted/updated row.
                BindIdentityParameters(selectIdCmd, it, normalizedComment);

                var idObj = await selectIdCmd.ExecuteScalarAsync(token);

                if (idObj is null || idObj == DBNull.Value)
                {
                    throw new InvalidOperationException(
                        $"Failed to resolve myCollection row id for identity: " +
                        $"Uuid={it.Uuid}, Language={it.Language}, " +
                        $"Finish={it.Finish}, Condition={it.Condition}, " +
                        $"LocationId={(it.LocationId?.ToString() ?? "null")}, " +
                        $"Comment={(normalizedComment ?? "null")}.");
                }

                var id = Convert.ToInt32(idObj);

                // 4. Return the mutation using the existing result semantics.
                result.Add(new CollectionCardDbRow
                {
                    CardId = id,
                    Identity = CollectionIdentityFactory.Create(
                        it.Uuid,
                        it.Condition,
                        it.Language,
                        it.Finish,
                        it.LocationId,
                        normalizedComment),
                    CardsOwned = it.CardsOwned,
                    CardsForTrade = it.CardsForTrade
                });

                percent?.Report((int)(((i + 1) / (double)items.Count) * 100));
            }

            return result;
        }
        private static void AddIdentityParameters(SQLiteCommand cmd)
        {
            cmd.Parameters.Add("@uuid", DbType.String);
            cmd.Parameters.Add("@language", DbType.String);
            cmd.Parameters.Add("@finish", DbType.String);
            cmd.Parameters.Add("@condition", DbType.String);
            cmd.Parameters.Add("@locationId", DbType.Int32);
            cmd.Parameters.Add("@comment", DbType.String);
        }
        private static void AddQuantityParameters(SQLiteCommand cmd)
        {
            cmd.Parameters.Add("@owned", DbType.Int32);
            cmd.Parameters.Add("@trade", DbType.Int32);
        }
        private static void BindIdentityParameters(SQLiteCommand cmd, CollectionUpsertItem item, string? normalizedComment)
        {
            cmd.Parameters["@uuid"].Value = item.Uuid;
            cmd.Parameters["@language"].Value = item.Language;
            cmd.Parameters["@finish"].Value = item.Finish;
            cmd.Parameters["@condition"].Value = item.Condition;
            cmd.Parameters["@locationId"].Value = SqliteDbHelpers.ToDbNullableInt(item.LocationId);
            cmd.Parameters["@comment"].Value = SqliteDbHelpers.ToDbNullableString(normalizedComment);
        }
        private static void BindQuantityParameters(SQLiteCommand cmd, CollectionUpsertItem item)
        {
            cmd.Parameters["@owned"].Value = item.CardsOwned;
            cmd.Parameters["@trade"].Value = item.CardsForTrade;
        }
    }
}

