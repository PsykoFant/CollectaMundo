using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.CardDatabaseManagement
{
    public interface IDatabaseIntegrityRepo
    {
        Task<bool> HasExpectedTablesAndViewsAsync(SQLiteConnection conn);
        Task<bool> HasOracleCardsAsync(SQLiteConnection conn);
        Task VerifyMainDatabaseIntegrityAsync(SQLiteConnection conn, SQLiteTransaction? tx);
        Task<bool> HasIncompletePreparationAsync(SQLiteConnection conn);
        Task MarkPreparationStartedAsync(SQLiteConnection conn, SQLiteTransaction tx);
        Task MarkPreparationCompletedAsync(SQLiteConnection conn, SQLiteTransaction tx);
    }

}
