using CollectaMundo.DomainLogic.Import.Models;
using CollectaMundo.DomainLogic.Shared.Factories;
using CollectaMundo.Infrastructure.Shared;
using CollectaMundo.Infrastructure.Shared.Models;
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
            var values = valuesEnumerable.ToList();

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
            var result = values.Distinct().ToDictionary(v => v, v => new List<string>());

            // CreateCollectionChangeSetFromEdits the parameterized SQL query that unions cardIdentifiers & tokenIdentifiers.
            // We'll create parameters @v0, @v1, ...
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
                else
                {
                    // Unexpected idval in results - create a bucket to be safe
                    result[idval] = [uuid];
                }
            }

            return result;
        }

        // step 3

        //  Name + SetCode Lookup
        public async Task<Dictionary<string, List<string>>> QueryByNameAndSetCodeAsync(SQLiteConnection conn, IReadOnlyList<(string Name, string SetCode)> pairs, CancellationToken token)
        {
            var result = new Dictionary<string, List<string>>();

            if (pairs.Count == 0)
            {
                return result;
            }

            // CreateCollectionChangeSetFromEdits parameterized IN clauses
            var nameParams = new List<SQLiteParameter>();
            var codeParams = new List<SQLiteParameter>();

            var namePlaceholders = new List<string>();
            var codePlaceholders = new List<string>();

            for (int i = 0; i < pairs.Count; i++)
            {
                var n = new SQLiteParameter($"@name{i}", pairs[i].Name);
                var s = new SQLiteParameter($"@setcode{i}", pairs[i].SetCode);

                nameParams.Add(n);
                codeParams.Add(s);

                namePlaceholders.Add(n.ParameterName);
                codePlaceholders.Add(s.ParameterName);
            }

            string nameInClause = string.Join(",", namePlaceholders);
            string codeInClause = string.Join(",", codePlaceholders);

            string sql = $@"
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
            ";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;

            foreach (var p in nameParams)
            {
                cmd.Parameters.Add(p);
            }

            foreach (var p in codeParams)
            {
                cmd.Parameters.Add(p);
            }

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

                // Normalize key to lower-case
                string key = $"{name}_{code}".ToLowerInvariant();

                if (!result.TryGetValue(key, out var list))
                {
                    list = new List<string>();
                    result[key] = list;
                }

                list.Add(uuid);
            }

            return result;
        }

        //  Name + SetName Lookup
        public async Task<Dictionary<string, List<string>>> QueryByNameAndSetNameAsync(SQLiteConnection conn, IReadOnlyList<(string Name, string SetName)> pairs, CancellationToken token)
        {
            var result = new Dictionary<string, List<string>>();

            if (pairs.Count == 0)
            {
                return result;
            }

            var nameParams = new List<SQLiteParameter>();
            var setParams = new List<SQLiteParameter>();

            var namePlaceholders = new List<string>();
            var setPlaceholders = new List<string>();

            for (int i = 0; i < pairs.Count; i++)
            {
                var n = new SQLiteParameter($"@name{i}", pairs[i].Name);
                var s = new SQLiteParameter($"@setname{i}", pairs[i].SetName);

                nameParams.Add(n);
                setParams.Add(s);

                namePlaceholders.Add(n.ParameterName);
                setPlaceholders.Add(s.ParameterName);
            }

            string nameInClause = string.Join(",", namePlaceholders);
            string setInClause = string.Join(",", setPlaceholders);

            string sql = $@"
                SELECT uuid, name, setName
                FROM view_cardToken
                WHERE name COLLATE NOCASE IN ({nameInClause})
                  AND setName COLLATE NOCASE IN ({setInClause})

                UNION ALL

                SELECT uuid, faceName AS name, setName
                FROM view_cardToken
                WHERE faceName COLLATE NOCASE IN ({nameInClause})
                  AND setName COLLATE NOCASE IN ({setInClause});
            ";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;

            foreach (var p in nameParams)
            {
                cmd.Parameters.Add(p);
            }

            foreach (var p in setParams)
            {
                cmd.Parameters.Add(p);
            }

            using var reader = await cmd.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                string uuid = reader["uuid"]?.ToString() ?? "";
                string name = reader["name"]?.ToString() ?? "";
                string setNm = reader["setName"]?.ToString() ?? "";

                if (string.IsNullOrWhiteSpace(uuid) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(setNm))
                {
                    continue;
                }

                string key = $"{name}_{setNm}".ToLowerInvariant();

                if (!result.TryGetValue(key, out var list))
                {
                    list = new List<string>();
                    result[key] = list;
                }

                list.Add(uuid);
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

            // CreateCollectionChangeSetFromEdits IN-clause parameters
            var nameParams = new List<SQLiteParameter>();
            var placeholders = new List<string>();

            for (int i = 0; i < names.Count; i++)
            {
                var p = new SQLiteParameter($"@name{i}", names[i]);
                nameParams.Add(p);
                placeholders.Add(p.ParameterName);
            }

            string nameInClause = string.Join(",", placeholders);

            // SQL covers:
            // - name match in view_cardToken
            // - faceName match (e.g., double-faced cards)
            string sql = $@"
                SELECT uuid, name
                FROM view_cardToken
                WHERE name COLLATE NOCASE IN ({nameInClause})

                UNION ALL

                SELECT uuid, faceName AS name
                FROM view_cardToken
                WHERE faceName COLLATE NOCASE IN ({nameInClause});
            ";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;

            foreach (var p in nameParams)
            {
                cmd.Parameters.Add(p);
            }

            using var reader = await cmd.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                string uuid = reader["uuid"]?.ToString() ?? "";
                string name = reader["name"]?.ToString() ?? "";

                if (uuid == "" || name == "")
                {
                    continue;
                }

                string key = name.ToLowerInvariant();

                if (!result.TryGetValue(key, out var list))
                {
                    list = [];
                    result[key] = list;
                }

                list.Add(uuid);
            }

            return result;
        }

        // Step 9
        public async Task<IReadOnlyDictionary<string, BaseAvailability>> FetchBaseAvailabilityAsync(IReadOnlyCollection<string> uuids, IDbConnection connection, IDbTransaction? tx, CancellationToken token)
        {
            // 1) Create + populate TEMP table temp_import_uuids
            await PrepareTempUuidsAsync(connection, tx, uuids, token);

            // 2) Query cards join temp
            var cards = await QueryBaseFromCardsAsync(connection, tx, token);

            // 3) Query tokens join temp
            var tokens = await QueryBaseFromTokensAsync(connection, tx, token);

            // 4) Merge (uuid unique across cards/tokens per your assumption F)
            return MergeBase(cards, tokens);
        }
        public async Task<IReadOnlyDictionary<string, HashSet<string>>> FetchForeignLanguagesAsync(IReadOnlyCollection<string> uuids, IDbConnection connection, IDbTransaction? tx, CancellationToken token)
        {
            // Create/populate temp_import_uuids with ONLY these uuids (or reuse and overwrite)
            await PrepareTempUuidsAsync(connection, tx, uuids, token);

            // Query foreign languages
            return await QueryForeignLanguagesAsync(connection, tx, token);
        }

        // --- TEMP TABLE SETUP ---
        private static async Task PrepareTempUuidsAsync(IDbConnection c,IDbTransaction? tx,IReadOnlyCollection<string> uuids,CancellationToken token)
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

        // --- BASE LOOKUPS (cards/tokens) ---
        private static async Task<List<BaseAvailability>> QueryBaseFromCardsAsync(IDbConnection c,IDbTransaction? tx,CancellationToken token)
        {
            var conn = (SQLiteConnection)c;
            var sqliteTx = (SQLiteTransaction?)tx;

            const string sql = """
                SELECT c.uuid, c.language, c.finishes
                FROM cards c
                JOIN temp_import_uuids t ON t.uuid = c.uuid;
                """;

            var results = new List<BaseAvailability>();

            using var cmd = new SQLiteCommand(sql, conn, sqliteTx);
            using var reader = await cmd.ExecuteReaderAsync(token);

            while (await reader.ReadAsync(token))
            {
                var uuid = reader["uuid"]?.ToString();
                if (string.IsNullOrWhiteSpace(uuid))
                    continue;

                var lang = reader["language"]?.ToString();
                var finishes = reader["finishes"]?.ToString();

                results.Add(new BaseAvailability(uuid, lang, finishes));
            }

            return results;
        }
        private static async Task<List<BaseAvailability>> QueryBaseFromTokensAsync(IDbConnection c,IDbTransaction? tx,CancellationToken token)
        {
            var conn = (SQLiteConnection)c;
            var sqliteTx = (SQLiteTransaction?)tx;

            const string sql = """
                SELECT t.uuid, t.language, t.finishes
                FROM tokens t
                JOIN temp_import_uuids u ON u.uuid = t.uuid;
                """;

            var results = new List<BaseAvailability>();

            using var cmd = new SQLiteCommand(sql, conn, sqliteTx);
            using var reader = await cmd.ExecuteReaderAsync(token);

            while (await reader.ReadAsync(token))
            {
                var uuid = reader["uuid"]?.ToString();
                if (string.IsNullOrWhiteSpace(uuid))
                    continue;

                var lang = reader["language"]?.ToString();
                var finishes = reader["finishes"]?.ToString();

                results.Add(new BaseAvailability(uuid, lang, finishes));
            }

            return results;
        }
        private static IReadOnlyDictionary<string, BaseAvailability> MergeBase(List<BaseAvailability> cards,List<BaseAvailability> tokens)
        {
            // Defensive merge: prefer cards if collision.
            var dict = new Dictionary<string, BaseAvailability>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in tokens)
            {
                if (!string.IsNullOrWhiteSpace(row.Uuid))
                    dict[row.Uuid] = row;
            }

            foreach (var row in cards)
            {
                if (!string.IsNullOrWhiteSpace(row.Uuid))
                    dict[row.Uuid] = row;
            }

            return dict;
        }

        // --- FOREIGN LANGUAGES LOOKUP (cardForeignData) ---
        private static async Task<IReadOnlyDictionary<string, HashSet<string>>> QueryForeignLanguagesAsync(IDbConnection c,IDbTransaction? tx,CancellationToken token)
        {
            var conn = (SQLiteConnection)c;
            var sqliteTx = (SQLiteTransaction?)tx;

            const string sql = """
                SELECT f.uuid, f.language
                FROM cardForeignData f
                JOIN temp_import_uuids t ON t.uuid = f.uuid;
                """;

            var dict = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            using var cmd = new SQLiteCommand(sql, conn, sqliteTx);
            using var reader = await cmd.ExecuteReaderAsync(token);

            while (await reader.ReadAsync(token))
            {
                token.ThrowIfCancellationRequested();

                var uuid = reader["uuid"]?.ToString();
                if (string.IsNullOrWhiteSpace(uuid))
                    continue;

                var lang = reader["language"]?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(lang))
                    continue;

                if (!dict.TryGetValue(uuid, out var set))
                {
                    set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    dict[uuid] = set;
                }

                set.Add(lang);
            }

            return dict;
        }


        // Final upsert step that inserts/updates myCollection based on the imported data, returning the resulting rows with their assigned CardIds. This is where we apply the "additive" logic for owned/trade counts.
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
                               SET cardsOwned    = cardsOwned + @owned,
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

            for (int i = 0; i < items.Count; i++)
            {
                token.ThrowIfCancellationRequested();

                var it = items[i];
                var normalizedComment = DbHelpers.NormalizeNullableString(it.Comment);

                using var updateCmd = new SQLiteCommand(updateSql, conn, tx);
                AddIdentityParameters(updateCmd);
                AddQuantityParameters(updateCmd);

                BindIdentityParameters(updateCmd, it, normalizedComment);
                BindQuantityParameters(updateCmd, it);

                var affectedRows = await updateCmd.ExecuteNonQueryAsync(token);

                if (affectedRows == 0)
                {
                    using var insertCmd = new SQLiteCommand(insertSql, conn, tx);
                    AddIdentityParameters(insertCmd);
                    AddQuantityParameters(insertCmd);

                    BindIdentityParameters(insertCmd, it, normalizedComment);
                    BindQuantityParameters(insertCmd, it);

                    await insertCmd.ExecuteNonQueryAsync(token);
                }

                object? idObj;
                using (var selectIdCmd = new SQLiteCommand(selectIdSql, conn, tx))
                {
                    AddIdentityParameters(selectIdCmd);
                    BindIdentityParameters(selectIdCmd, it, normalizedComment);

                    idObj = await selectIdCmd.ExecuteScalarAsync(token);
                }

                if (idObj is null || idObj == DBNull.Value)
                {
                    throw new InvalidOperationException($"Failed to resolve myCollection row id for identity: " +
                        $"Uuid={it.Uuid}, Language={it.Language}, Finish={it.Finish}, Condition={it.Condition}, " +
                        $"LocationId={(it.LocationId?.ToString() ?? "null")}, Comment={(normalizedComment ?? "null")}.");
                }

                var id = Convert.ToInt32(idObj);

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
            cmd.Parameters["@locationId"].Value = DbHelpers.ToDbNullableInt(item.LocationId);
            cmd.Parameters["@comment"].Value = DbHelpers.ToDbNullableString(normalizedComment);
        }
        private static void BindQuantityParameters(SQLiteCommand cmd, CollectionUpsertItem item)
        {
            cmd.Parameters["@owned"].Value = item.CardsOwned;
            cmd.Parameters["@trade"].Value = item.CardsForTrade;
        }
    }    
}

