using CollectaMundo.DomainLogic.Models;

namespace CollectaMundo.Data
{
    public interface IEditCollectionRepository
    {
        // Lookups (with db open/closed)        
        Task<List<string>> FetchLanguagesForCardAsync(string uuid);
        Task<List<string>> FetchFinishesForCardAsync(string uuid);

        // Lookups (without db open/closed)
        Task<int?> FindExistingCardReturnIdAsync(CardSet card);
        Task<List<int>> FindRecordByIdAsync(string uuid, string condition, string language, string finish);

        // CRUD
        Task AddCardAsync(CardSet card);
        Task UpdateCardAsync(CardSet card);
        Task UpdateCardCountsAsync(CardSet card);
        Task DeleteCardByIdAsync(CardSet card);
        Task<(int sumOwned, int sumTrade)> MergeDuplicateRecordsAsync(string uuid, string condition, string language, string finish, int keepId);
    }
}
