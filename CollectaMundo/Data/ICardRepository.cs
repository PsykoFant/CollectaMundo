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


        //// Checks if a card with the same unique properties already exists.
        //Task<int?> CheckForExistingCardAsync(CardSet card);

        //// Inserts a new card into the collection.
        //Task AddCardAsync(CardSet card);

        //// Updates an existing card in the collection.
        //Task UpdateCardAsync(CardSet card);

        //// Deletes a card from the collection.
        //Task DeleteCardAsync(CardSet card);

        //// Fetches the list of languages for a card.
        //Task<List<string>> FetchLanguagesForCardAsync(string uuid);

        //// Fetches the list of finishes for a card.
        //Task<List<string>> FetchFinishesForCardAsync(string uuid);
    }
}
