using CollectaMundo.DomainLogic.KeyedDataProvider;
using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.KeyedDataProvider
{
    public interface IKeyedDataProviderService
    {
        // Ensures data providers exist (no-op if already initialized).
        Task<KeyedDataProviderPackage> LoadKeyedDataAsync(SQLiteConnection conn, KeyedDataProviderOptions opts);
        Task ResetPricesMetaProviderAsync(string retailerKey);
    }
}
