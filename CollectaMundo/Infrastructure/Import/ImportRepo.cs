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

    }
}
