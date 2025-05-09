using CollectaMundo.DomainLogic.Models;

namespace CollectaMundo.Data
{
    public interface IEditCollectionRepository
    {

        // Lookups
        Task<int?> CheckForExistingCardAsync(CardSet card);
        Task<List<string>> FetchLanguagesForCardAsync(string uuid);
        Task<List<string>> FetchFinishesForCardAsync(string uuid);
        Task<CardSet> GetMyCollectionRecordAsync(string uuid, string condition, string language, string finish);

        // CRUD
        Task AddCardAsync(CardSet card);
        Task EditCardAsync(CardSet card);
        Task UpdateCardCountsAsync(CardSet card);
        Task DeleteCardAsync(CardSet card);
        Task MergeDuplicateRecordsAsync(string uuid, string condition, string language, string finish);
    }
}
