using CollectaMundo.DomainLogic.CardLists.Lookups;
using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.CardLists.Lookups
{
    public interface ICardLookupsService
    {
        // Ensures data providers exist (no-op if already initialized).
        Task<CardLookupPackage> LoadLookupDataAsync(SQLiteConnection conn, CardLookupsOptions opts);
        Task ReloadPricesAsync(SQLiteConnection conn, string retailerKey);
    }
}
