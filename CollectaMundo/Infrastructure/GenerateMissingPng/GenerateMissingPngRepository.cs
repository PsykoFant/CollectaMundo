using CollectaMundo.Infrastructure.Common;
using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.GenerateMissingPng
{
    public class GenerateMissingPngRepository : IGenerateMissingPngRepository
    {
        public async Task<List<string>> GetUniqueValuesAsync(SQLiteConnection conn, string tableName, string columnName)
        {
            return await DbHelpers.GetUniqueValuesAsync(conn, tableName, columnName);
        }
        public async Task<List<string>> GetValuesWithNullAsync(SQLiteConnection conn, string tableName, string returnColumn, string targetColumn)
        {
            List<string> results = [];

            string query = $@"
                    SELECT {returnColumn}
                    FROM {tableName}
                    WHERE {targetColumn} IS NULL 
                      AND {returnColumn} IS NOT NULL 
                      AND {returnColumn} != '';";

            using var command = new SQLiteCommand(query, conn);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                string? value = reader[returnColumn]?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    results.Add(value);
                }
            }

            return results;
        }
        public async Task<bool> UpdateImageAsync(SQLiteConnection conn, string tableName, string imageColumn, string referenceColumn, string referenceValue, byte[] imageData)
        {
            string query = $@"
                    UPDATE {tableName}
                    SET {imageColumn} = @imageData
                    WHERE {referenceColumn} = @referenceValue
                      AND {imageColumn} IS NULL;";

            using var command = new SQLiteCommand(query, conn);
            command.Parameters.AddWithValue("@imageData", imageData);
            command.Parameters.AddWithValue("@referenceValue", referenceValue);

            int rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }
        public async Task<bool> UpdateKeyruneImageAsync(SQLiteConnection conn, string setCode, byte[] imageData, bool usedDefaultSvg)
        {
            const string query = @"UPDATE keyruneImages
                                    SET keyruneImage = @imageData,defaultSvgUsed = @usedDefaultSvg
                                    WHERE setCode = @setCode
                                    AND keyruneImage IS NULL;";

            using var command = new SQLiteCommand(query, conn);
            command.Parameters.AddWithValue("@imageData", imageData);
            command.Parameters.AddWithValue("@setCode", setCode);
            command.Parameters.AddWithValue("@usedDefaultSvg", usedDefaultSvg ? 1 : 0);

            int rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }
        public async Task InsertIfNotExistsAsync(SQLiteConnection conn, string tableName, string columnName, string value)
        {
            string query = $@"
                    INSERT OR IGNORE INTO {tableName} ({columnName})
                    VALUES (@value);";

            using var command = new SQLiteCommand(query, conn);
            command.Parameters.AddWithValue("@value", value);

            await command.ExecuteNonQueryAsync();
        }
        public async Task<Dictionary<string, byte[]>> GetManaSymbolImagesAsync(SQLiteConnection conn, IEnumerable<string> symbols)
        {
            var result = new Dictionary<string, byte[]>();

            var symbolList = symbols.ToList();
            if (symbolList.Count == 0)
            {
                return result;
            }

            string paramList = string.Join(",", symbolList.Select((_, i) => $"@p{i}"));
            string query = $@"
                    SELECT uniqueManaSymbol, manaSymbolImage
                    FROM uniqueManaSymbols
                    WHERE uniqueManaSymbol IN ({paramList});";

            using var command = new SQLiteCommand(query, conn);
            for (int i = 0; i < symbolList.Count; i++)
            {
                command.Parameters.AddWithValue($"@p{i}", symbolList[i]);
            }

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string symbol = reader.GetString(0);
                byte[] imageBytes = (byte[])reader["manaSymbolImage"];
                result[symbol] = imageBytes;
            }

            return result;
        }
        public async Task InsertMissingFromColumnAsync(SQLiteConnection conn, string fromTable, string fromColumn, string intoTable, string intoColumn)
        {
            string query = $@"
                    INSERT INTO {intoTable} ({intoColumn})
                    SELECT DISTINCT {fromColumn}
                    FROM {fromTable}
                    WHERE {fromColumn} IS NOT NULL AND {fromColumn} != ''
                    EXCEPT
                    SELECT DISTINCT {intoColumn}
                    FROM {intoTable}
                    WHERE {intoColumn} IS NOT NULL AND {intoColumn} != '';";

            using var command = new SQLiteCommand(query, conn);
            await command.ExecuteNonQueryAsync();
        }
        public async Task DeleteWhereDefaultSvgUsedAsync(SQLiteConnection conn)
        {
            string query = $@"
                            UPDATE keyruneImages 
                            SET keyruneImage = NULL, defaultSvgUsed = 0
                            WHERE defaultSvgUsed = 1;";
            using var command = new SQLiteCommand(query, conn);
            await command.ExecuteNonQueryAsync();
        }

    }
}
