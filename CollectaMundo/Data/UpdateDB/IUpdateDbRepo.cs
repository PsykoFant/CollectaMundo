using System.Data.SQLite;

namespace CollectaMundo.Data.UpdateDB
{
    public interface IUpdateDbRepo
    {
        Task<int> GetNumberOfSetsAsync(SQLiteConnection conn);
        Task CopyTablesFromNewDbAsync(SQLiteConnection conn, IProgress<string> progress, string newDbPath);
    }
}
