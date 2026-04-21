using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.ModifyCollection
{
    public interface IModifyCollectionRepo
    {
        // Lookups
        Task<List<string>> FetchLanguagesForCardAsync(string uuid, SQLiteConnection conn);
        Task<List<string>> FetchFinishesForCardAsync(string uuid, SQLiteConnection conn);
    }
}
