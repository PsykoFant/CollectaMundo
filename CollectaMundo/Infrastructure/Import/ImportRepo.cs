using System.Data.SQLite;

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

    }
}
