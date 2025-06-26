using System.Data.SQLite;

namespace CollectaMundo.Data.ImportExport
{
    public interface IImportExportRepo
    {
        void DummyTask();
        Task<string?> ExportCollectionAsync(SQLiteConnection conn);
    }
}
