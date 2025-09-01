using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.CardLists
{
    public interface ICardLookupsService
    {
        /// Ensures data providers exist (no-op if already initialized).
        Task InitializeAsync(SQLiteConnection conn, CardLookupsOptions opts);
    }
}
