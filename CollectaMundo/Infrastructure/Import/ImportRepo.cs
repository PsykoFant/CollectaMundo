using CollectaMundo.DomainLogic.Import.Models;
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

            // Build the parameterized SQL query that unions cardIdentifiers & tokenIdentifiers.
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

            // Build parameterized IN clauses
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

            // Build IN-clause parameters
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
        public async Task UpsertMyCollectionAsync(IReadOnlyList<CollectionUpsertItem> items, SQLiteConnection conn, SQLiteTransaction tx, IProgress<int>? percent, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(items);

            ArgumentNullException.ThrowIfNull(conn);

            ArgumentNullException.ThrowIfNull(tx);

            if (items.Count == 0)
            {
                percent?.Report(100);
                return;
            }

            // SQLite UPSERT requires a UNIQUE constraint/index on the conflict target:
            //   (uuid, language, finish, condition)
            const string sql = @"
                INSERT INTO myCollection
                    (uuid, language, finish, condition, cardsOwned, cardsForTrade)
                VALUES
                    (@uuid, @language, @finish, @condition, @owned, @trade)
                ON CONFLICT(uuid, language, finish, condition) DO UPDATE SET
                    cardsOwned    = cardsOwned + excluded.cardsOwned,
                    cardsForTrade = cardsForTrade + excluded.cardsForTrade;
                ";

            // Update progress at a reasonable interval (avoid UI spam)
            const int reportEvery = 250;

            // Prepared statement with reusable parameters
            using var cmd = new SQLiteCommand(sql, conn, tx);

            var pUuid = cmd.Parameters.Add("@uuid", DbType.String);
            var pLanguage = cmd.Parameters.Add("@language", DbType.String);
            var pFinish = cmd.Parameters.Add("@finish", DbType.String);
            var pCondition = cmd.Parameters.Add("@condition", DbType.String);
            var pOwned = cmd.Parameters.Add("@owned", DbType.Int32);
            var pTrade = cmd.Parameters.Add("@trade", DbType.Int32);

            // Optional: if you expect very high volume, bump command timeout
            // cmd.CommandTimeout = 60;

            int total = items.Count;

            for (int i = 0; i < total; i++)
            {
                token.ThrowIfCancellationRequested();

                var it = items[i];

                // Defensive: your Collapser should already guarantee these are valid
                // but keep the repo resilient.
                if (string.IsNullOrWhiteSpace(it.Uuid))
                {
                    continue;
                }

                pUuid.Value = it.Uuid;
                pLanguage.Value = it.Language;
                pFinish.Value = it.Finish;
                pCondition.Value = it.Condition;
                pOwned.Value = it.CardsOwned;
                pTrade.Value = it.CardsForTrade;

                await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);

                // Progress
                if (percent != null && (i % reportEvery == 0 || i == total - 1))
                {
                    int pct = (int)(((i + 1) / (double)total) * 100);
                    percent.Report(pct);
                }
            }
        }
    }
}
