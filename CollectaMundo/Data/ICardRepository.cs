using CollectaMundo.Models;

namespace CollectaMundo.Data
{
    public interface ICardRepository
    {

        // Raw lookups
        Task<int?> CheckForExistingCardAsync(CardSet card);
        Task<List<string>> FetchLanguagesForCardAsync(string uuid);
        Task<List<string>> FetchFinishesForCardAsync(string uuid);

        // Raw CRUD
        Task AddCardAsync(CardSet card);
        Task UpdateCardAsync(CardSet card);
        Task DeleteCardAsync(CardSet card);
    }
}
