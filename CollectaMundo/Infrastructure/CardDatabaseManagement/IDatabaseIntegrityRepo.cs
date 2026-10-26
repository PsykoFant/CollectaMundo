using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.CardDatabaseManagement
{
    public interface IDatabaseIntegrityRepo
    {
        Task<bool> HasExpectedTablesAndViewsAsync(SQLiteConnection conn);
        Task<bool> HasOracleCardsAsync(SQLiteConnection conn);
        Task<bool> QuickCheckAsync(SQLiteConnection conn);
    }

}
