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



        /// <summary>Fetches all IDs whose key matches.</summary>
        Task<List<int>> GetMatchingRecordIdsAsync(
            string uuid,
            string condition,
            string language,
            string finish);

        /// <summary>Merges duplicates in the DB into one survivor.</summary>
        Task MergeDuplicateRecordsAsync(
            string uuid,
            string condition,
            string language,
            string finish);
    }
}
