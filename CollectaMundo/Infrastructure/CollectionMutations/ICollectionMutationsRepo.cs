using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.CollectionMutations
{
    public interface ICollectionMutationsRepo
    {
        Task<int> AddCardAndReturnIdAsync(string uuid, string condition, string language, string finish, int? locationId, string? comment, int cardsOwned, int cardsForTrade, SQLiteConnection conn);
        Task DeleteCardByIdAsync(int cardId, SQLiteConnection conn);
        Task UpdateCardFieldsByIdAsync(int id, int owned, int trade, string condition, string language, string finish, int? locationId, string? comment, SQLiteConnection conn);
    }
}
