using System.Data.SQLite;

namespace CollectaMundo.Data.CardDatabaseManagement
{
    public interface IDatabaseIntegrityRepo
    {
        Task<bool> HasExpectedTablesAndViewsAsync(SQLiteConnection conn);
        Task<bool> QuickCheckAsync(SQLiteConnection conn);
    }

}
