using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.CardLocations
{
    public interface ICardLocationReferenceCleanupService
    {
        Task CleanupBeforeLocationDeleteAsync(SQLiteConnection conn, SQLiteTransaction tx, int locationId);
    }
}
