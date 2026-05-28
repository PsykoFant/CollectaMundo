using System.Data;
using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.Shared
{
    public static class DbHelpers
    {
        public static SQLiteCommand CreateCommand(SQLiteConnection conn, SQLiteTransaction? tx, string sql)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Transaction = tx;
            return cmd;
        }
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
        public static object ToDbNullableInt(int? value)
        {
            return value.HasValue
                ? value.Value
                : DBNull.Value;
        }
        public static object ToDbNullableString(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? DBNull.Value
                : value.Trim();
        }
        public static string? NormalizeNullableString(string? value)
        {
            var trimmed = value?.Trim();

            return string.IsNullOrWhiteSpace(trimmed)
                ? null
                : trimmed;
        }
        public static SQLiteParameter AddInt32(SQLiteCommand cmd, string name, int value)
        {
            return cmd.Parameters.Add(name, DbType.Int32).WithValue(value);
        }
        public static SQLiteParameter AddNullableInt32(SQLiteCommand cmd, string name, int? value)
        {
            return cmd.Parameters.Add(name, DbType.Int32).WithValue(ToDbNullableInt(value));
        }
        public static SQLiteParameter AddString(SQLiteCommand cmd, string name, string value)
        {
            return cmd.Parameters.Add(name, DbType.String).WithValue(value);
        }
        public static SQLiteParameter AddNullableString(SQLiteCommand cmd, string name, string? value)
        {
            return cmd.Parameters.Add(name, DbType.String).WithValue(ToDbNullableString(value));
        }
        private static SQLiteParameter WithValue(this SQLiteParameter parameter, object value)
        {
            parameter.Value = value;
            return parameter;
        }
        public static async Task<bool> ExistsAsync(SQLiteCommand cmd, CancellationToken ct = default)
        {
            object? scalar = await cmd.ExecuteScalarAsync(ct);
            return scalar is not null && scalar != DBNull.Value;
        }
    }
}

