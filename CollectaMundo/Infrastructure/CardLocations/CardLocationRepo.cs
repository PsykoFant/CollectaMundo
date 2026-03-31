using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.CardLocations
{
    public sealed class CardLocationRepo : ICardLocationRepo
    {
        public async Task<IReadOnlyList<CardLocationRecord>> GetAllAsync(SQLiteConnection conn)
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
        public async Task<int> InsertAsync(SQLiteConnection conn, string name, string type)
        {
            const string sql = """
                INSERT INTO cardLocations (name, type)
                VALUES (@name, @type);

                SELECT last_insert_rowid();
                """;

            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@type", type);

            object? scalar = await cmd.ExecuteScalarAsync();

            if (scalar is null || scalar == DBNull.Value)
            {
                throw new InvalidOperationException("InsertAsync failed to return a new card location id.");
            }

            return Convert.ToInt32(scalar);
        }
        public async Task<int> UpdateAsync(SQLiteConnection conn, int id, string name, string type)
        {
            const string sql = """
                UPDATE cardLocations
                SET name = @name,
                    type = @type
                WHERE id = @id;
                """;

            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@type", type);

            return await cmd.ExecuteNonQueryAsync();
        }
        public async Task<int> DeleteAsync(SQLiteConnection conn, int id)
        {
            const string sql = """
                DELETE FROM cardLocations
                WHERE id = @id;
                """;

            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", id);

            return await cmd.ExecuteNonQueryAsync();
        }
        public async Task<bool> ExistsByNameAsync(SQLiteConnection conn, string name, int? excludingId = null)
        {
            const string sql = """
                SELECT 1
                FROM cardLocations
                WHERE name = @name COLLATE NOCASE
                  AND (@excludingId IS NULL OR id <> @excludingId)
                LIMIT 1;
                """;

            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@name", name);

            var excludingIdParam = cmd.Parameters.AddWithValue("@excludingId", excludingId ?? (object)DBNull.Value);
            excludingIdParam.Value = excludingId.HasValue ? excludingId.Value : DBNull.Value;

            object? scalar = await cmd.ExecuteScalarAsync();
            return scalar is not null && scalar != DBNull.Value;
        }
    }
}
