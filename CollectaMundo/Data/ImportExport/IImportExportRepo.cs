using System.Data.SQLite;

namespace CollectaMundo.Data.ImportExport
{
    public interface IImportExportRepo
    {
        Task<string?> ExportCollectionAsync(SQLiteConnection conn);
    }
}
