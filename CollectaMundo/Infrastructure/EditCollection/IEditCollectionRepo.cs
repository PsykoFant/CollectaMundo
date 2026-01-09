using CollectaMundo.DomainLogic.CardLists.Models;
using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.EditCollection
{
    public interface IEditCollectionRepo
    {
        Task<List<string>> FetchLanguagesForCardAsync(string uuid, SQLiteConnection conn);
        Task<List<string>> FetchFinishesForCardAsync(string uuid, SQLiteConnection conn);
        Task<int?> FindExistingCardReturnIdAsync(CardSet card, SQLiteConnection conn);
        Task<List<int>> FindRecordByIdAsync(string uuid, string condition, string language, string finish, SQLiteConnection conn);
        Task<(int TotalOwned, int TotalTrade)> GetTotalsAsync(string uuid, string condition, string language, string finish, SQLiteConnection conn);

        // CRUD
        Task<int> AddCardAndReturnIdAsync(CardSet card, SQLiteConnection conn);
        Task UpdateCardAsync(CardSet card, SQLiteConnection conn);
        Task UpdateCardCountsAsync(CardSet card, SQLiteConnection conn);
        Task DeleteCardByIdAsync(CardSet card, SQLiteConnection conn);
        Task DeleteCardsByIdsAsync(IEnumerable<int> ids, SQLiteConnection conn);

        Task UpdateCardFieldsByIdAsync(int id, int owned, int trade, string condition, string language, string finish, SQLiteConnection conn);
    }
}
