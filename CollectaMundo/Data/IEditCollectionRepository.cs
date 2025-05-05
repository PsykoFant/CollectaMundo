using CollectaMundo.DomainLogic.Models;

namespace CollectaMundo.Data
{
    public interface IEditCollectionRepository
    {

        // Raw lookups
        Task<int?> CheckForExistingCardAsync(CardSet card);
        Task<List<string>> FetchLanguagesForCardAsync(string uuid);
        Task<List<string>> FetchFinishesForCardAsync(string uuid);
        Task<CardSet> GetMyCollectionRecordAsync(
        string uuid,
        string condition,
        string language,
        string finish);

        // Raw CRUD
        Task AddCardAsync(CardSet card);
        Task UpdateCardAsync(CardSet card);
        Task DeleteCardAsync(CardSet card);
    }
}
