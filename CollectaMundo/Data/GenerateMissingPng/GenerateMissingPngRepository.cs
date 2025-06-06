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
                string query = $"SELECT DISTINCT {columnName} FROM {tableName};";

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
        public async Task UpdateImageAsync(SQLiteConnection conn, string tableName, string imageColumn, string referenceColumn, string referenceValue, byte[] imageData)
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
                //Debug.WriteLine($"[PngRepo] Updated image for '{referenceValue}' in '{tableName}'. Rows affected: {rowsAffected}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PngRepo] Error in UpdateImageAsync for {referenceValue}: {ex.Message}");
            }
        }
        public async Task InsertIfNotExistsAsync(SQLiteConnection conn, string tableName, string columnName, string value)
        {
            try
            {
                // First check if the value already exists
                string existsQuery = $@"
                    SELECT COUNT(*) 
                    FROM {tableName} 
                    WHERE {columnName} = @value;";

                using var existsCommand = new SQLiteCommand(existsQuery, conn);
                existsCommand.Parameters.AddWithValue("@value", value);

                var count = Convert.ToInt32(await existsCommand.ExecuteScalarAsync());

                if (count == 0)
                {
                    string insertQuery = $@"
                        INSERT INTO {tableName} ({columnName}) 
                        VALUES (@value);";

                    using var insertCommand = new SQLiteCommand(insertQuery, conn);
                    insertCommand.Parameters.AddWithValue("@value", value);

                    await insertCommand.ExecuteNonQueryAsync();
                    //Debug.WriteLine($"[PngRepo] Inserted new value '{value}' into {tableName}.");
                }
                else
                {
                    Debug.WriteLine($"[PngRepo] Value '{value}' already exists in {tableName}, skipping insert.");
                }
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
                string paramList = string.Join(",", symbols.Select((s, i) => $"@p{i}"));
                string query = $"SELECT uniqueManaSymbol, manaSymbolImage FROM uniqueManaSymbols WHERE uniqueManaSymbol IN ({paramList})";

                using var command = new SQLiteCommand(query, conn);

                int i = 0;
                foreach (string symbol in symbols)
                {
                    command.Parameters.AddWithValue($"@p{i++}", symbol);
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
        public async Task CopyColumnIfEmptyOrAddMissingRowsAsync(SQLiteConnection conn, string targetTable, string targetColumn, string sourceTable, string sourceColumn)
        {
            try
            {
                string query = $@"
                    INSERT INTO {targetTable} ({targetColumn})
                    SELECT DISTINCT {sourceColumn}
                    FROM {sourceTable} 
                    WHERE {sourceColumn} IS NOT NULL 
                      AND {sourceColumn} != '' 
                      AND {sourceColumn} NOT IN (
                          SELECT DISTINCT {targetColumn} 
                          FROM {targetTable} 
                          WHERE {targetColumn} IS NOT NULL AND {targetColumn} != ''
                      );";

                using var command = new SQLiteCommand(query, conn);
                int rowsAffected = await command.ExecuteNonQueryAsync();
                //Debug.WriteLine($"[PngRepo] {rowsAffected} rows copied from {sourceTable}.{sourceColumn} to {targetTable}.{targetColumn}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PngRepo] Error copying values from {sourceTable} to {targetTable}: {ex.Message}");
            }
        }

    }
}
