using System.Data.SQLite;

namespace CollectaMundo.Data.UpdateDB
{
    public interface IUpdateDbRepo
    {
        Task<int> GetNumberOfSetsAsync(SQLiteConnection conn);
    }
}
