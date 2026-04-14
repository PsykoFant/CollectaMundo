using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.ModifyCollection
{
    public interface IModifyCollectionRepo
    {
        // Lookups
        Task<List<string>> FetchLanguagesForCardAsync(string uuid, SQLiteConnection conn);
        Task<List<string>> FetchFinishesForCardAsync(string uuid, SQLiteConnection conn);

        // CRUD
        Task<int> AddCardAndReturnIdAsync(string uuid, string condition, string language, string finish, int? locationId, string? comment, int cardsOwned, int cardsForTrade, SQLiteConnection conn);
        Task DeleteCardByIdAsync(int cardId, SQLiteConnection conn);
        Task UpdateCardFieldsByIdAsync(int id, int owned, int trade, string condition, string language, string finish, int? locationId, string? comment, SQLiteConnection conn);
    }
}
