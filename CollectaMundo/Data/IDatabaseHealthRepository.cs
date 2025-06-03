using System.Data.SQLite;

namespace CollectaMundo.Data
{
    public interface IDatabaseHealthRepository
    {
        Task<bool> HasExpectedTablesAndViewsAsync(SQLiteConnection conn);
        Task<bool> QuickCheckAsync(SQLiteConnection conn);
    }

}
