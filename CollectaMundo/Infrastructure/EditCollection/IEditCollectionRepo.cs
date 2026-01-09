using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.EditCollection.Models;
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
        Task MergeDuplicateRecordsAsync(string uuid, string condition, string language, string finish, int keepId, SQLiteConnection conn);
        Task<CardChangeEventArgs> UpdateOrMergeAsync(CardSet card, SQLiteConnection conn, SQLiteTransaction tx);
    }
}
