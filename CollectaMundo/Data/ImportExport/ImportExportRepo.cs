using System.Data.SQLite;
using System.IO;
using System.Text;

namespace CollectaMundo.Data.ImportExport
{
    public class ImportExportRepo() : IImportExportRepo
    {
        public async Task<string?> ExportCollectionAsync(SQLiteConnection conn, string backupFolderPath)
        {
            Directory.CreateDirectory(backupFolderPath);

            using var command = new SQLiteCommand("SELECT * FROM myCollection", conn);
            using var reader = await command.ExecuteReaderAsync();

            if (!reader.HasRows)
            {
                return null; // Signal: nothing to export
            }

            string filePath = Path.Combine(backupFolderPath, $"MyCollection_backup_{DateTime.Now:yyyyMMdd}.csv");
            using var writer = new StreamWriter(filePath, false, Encoding.UTF8);

            // Write header
            for (int i = 0; i < reader.FieldCount; i++)
            {
                writer.Write(reader.GetName(i));
                if (i < reader.FieldCount - 1)
                {
                    writer.Write(";");
                }
            }
            writer.WriteLine();

            // Write rows
            while (await reader.ReadAsync())
            {
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    string value = reader[i]?.ToString()?.Replace(";", ",") ?? string.Empty;
                    writer.Write(value);
                    if (i < reader.FieldCount - 1)
                    {
                        writer.Write(";");
                    }
                }
                writer.WriteLine();
            }

            return filePath; // Success
        }

    }
}
