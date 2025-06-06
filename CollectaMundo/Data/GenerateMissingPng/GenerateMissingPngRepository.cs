using System.Data.SQLite;
using System.Diagnostics;

namespace CollectaMundo.Data.GenerateMissingPng
{
    public class GenerateMissingPngRepository : IGenerateMissingPngRepository
    {
        public async Task<List<string>> GetUniqueValuesAsync(SQLiteConnection conn, string tableName, string columnName)
        {
            List<string> uniqueValues = [];

            try
            {
                string query = $@"
                    SELECT DISTINCT {columnName}
                    FROM {tableName}
                    WHERE {columnName} IS NOT NULL AND {columnName} != '';";

                using var command = new SQLiteCommand(query, conn);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    string? value = reader[columnName]?.ToString();
                    if (!string.IsNullOrWhiteSpace(value))
                        uniqueValues.Add(value);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PngRepo] Error fetching unique values from {tableName}.{columnName}: {ex.Message}");
            }

            return uniqueValues;
        }
        public async Task<List<string>> GetValuesWithNullAsync(SQLiteConnection conn, string tableName, string returnColumn, string targetColumn)
        {
            List<string> results = [];

            try
            {
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
                        results.Add(value);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PngRepo] Error in GetValuesWithNullAsync({tableName}): {ex.Message}");
            }

            return results;
        }
        public async Task<bool> UpdateImageAsync(SQLiteConnection conn, string tableName, string imageColumn, string referenceColumn, string referenceValue, byte[] imageData)
        {
            try
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
            catch (Exception ex)
            {
                Debug.WriteLine($"[PngRepo] Error in UpdateImageAsync for {referenceValue}: {ex.Message}");
                return false;
            }
        }
        public async Task InsertIfNotExistsAsync(SQLiteConnection conn, string tableName, string columnName, string value)
        {
            try
            {
                string query = $@"
                    INSERT OR IGNORE INTO {tableName} ({columnName})
                    VALUES (@value);";

                using var command = new SQLiteCommand(query, conn);
                command.Parameters.AddWithValue("@value", value);

                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PngRepo] Error inserting '{value}' into {tableName}.{columnName}: {ex.Message}");
            }
        }
        public async Task<Dictionary<string, byte[]>> GetManaSymbolImagesAsync(SQLiteConnection conn, IEnumerable<string> symbols)
        {
            var result = new Dictionary<string, byte[]>();

            try
            {
                var symbolList = symbols.ToList();
                if (symbolList.Count == 0)
                    return result;

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
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PngRepo] Error in GetManaSymbolImagesAsync: {ex.Message}");
            }

            return result;
        }
        public async Task InsertMissingFromColumnAsync(SQLiteConnection conn, string fromTable, string fromColumn, string intoTable, string intoColumn)
        {
            try
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
            catch (Exception ex)
            {
                Debug.WriteLine($"[PngRepo] Error inserting missing from {fromTable}.{fromColumn} → {intoTable}.{intoColumn}: {ex.Message}");
            }
        }
    }
}
