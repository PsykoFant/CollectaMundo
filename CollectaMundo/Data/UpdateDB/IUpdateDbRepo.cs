using System.Data.SQLite;

namespace CollectaMundo.Data.UpdateDB
{
    public interface IUpdateDbRepo
    {
        Task<int> GetNumberOfSetsAsync(SQLiteConnection conn);
        Task AttachTempDbAsync(SQLiteConnection conn, string newDbPath, IProgress<string> progress);
        Task DropTablesAsync(SQLiteConnection conn, IProgress<string> progress);
        Task CopyTablesAsync(SQLiteConnection conn, IProgress<string> progress);
        Task DetachTempDbAsync(SQLiteConnection conn, IProgress<string> progress);
    }
}
