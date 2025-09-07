using System.Data.SQLite;

namespace CollectaMundo.Data.Common
{
    public static class DbHelpers
    {
        public static async Task<List<string>> GetUniqueValuesAsync(SQLiteConnection conn, string tableName, string columnName, CancellationToken ct = default)
        {
            var uniqueValues = new List<string>();

            string query = $@"
                SELECT DISTINCT {columnName}
                FROM {tableName}
                WHERE {columnName} IS NOT NULL AND {columnName} != '';";

            using var command = new SQLiteCommand(query, conn);
            using var reader = await command.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
            {
                var value = reader[columnName]?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    uniqueValues.Add(value);
                }
            }

            return uniqueValues;
        }

    }
}

