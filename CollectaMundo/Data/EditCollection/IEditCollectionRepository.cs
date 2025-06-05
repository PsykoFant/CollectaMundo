using CollectaMundo.DomainLogic.CardLists.Models;

namespace CollectaMundo.Data.EditCollection
{
    public interface IEditCollectionRepository
    {
        Task<List<string>> FetchLanguagesForCardAsync(string uuid);
        Task<List<string>> FetchFinishesForCardAsync(string uuid);
        Task<int?> FindExistingCardReturnIdAsync(CardSet card);
        Task<List<int>> FindRecordByIdAsync(string uuid, string condition, string language, string finish);
        Task<(int TotalOwned, int TotalTrade)> GetTotalsAsync(string uuid, string condition, string language, string finish);

        // CRUD
        Task<int> AddCardAndReturnIdAsync(CardSet card);
        Task UpdateCardAsync(CardSet card);
        Task UpdateCardCountsAsync(CardSet card);
        Task DeleteCardByIdAsync(CardSet card);
        Task MergeDuplicateRecordsAsync(string uuid, string condition, string language, string finish, int keepId);
    }
}
