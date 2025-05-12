using CollectaMundo.DomainLogic.Models;

namespace CollectaMundo.Data
{
    public interface IEditCollectionRepository
    {

        // Lookups
        Task<int?> FindExistingCardReturnIdAsync(CardSet card);
        Task<List<string>> FetchLanguagesForCardAsync(string uuid);
        Task<List<string>> FetchFinishesForCardAsync(string uuid);
        Task<CardSet> FindExistingCardReturnRecordAsync(string uuid, string condition, string language, string finish);
        Task<List<int>> FindRecordByIdAsync(string uuid, string condition, string language, string finish);

        // CRUD
        Task AddCardAsync(CardSet card);
        Task UpdateCardAsync(CardSet card);
        Task UpdateCardCountsAsync(CardSet card);
        Task DeleteCardByIdAsync(CardSet card);
        Task MergeDuplicateRecordsAsync(string uuid, string condition, string language, string finish, int keepId);
    }
}
