using CollectaMundo.ApplicationServices.CardDatabaseManagement.Models;
using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.CardDatabaseManagement
{
    public interface IDatabaseIntegrityService
    {
        Task<DatabaseStatus> GetDatabaseStatusAsync();
        Task VerifyMainDatabaseIntegrityAsync(SQLiteConnection conn, SQLiteTransaction? tx);
        Task MarkFirstTimePreparationStartedAsync();
        Task MarkFirstTimePreparationCompletedAsync();
    }
}
