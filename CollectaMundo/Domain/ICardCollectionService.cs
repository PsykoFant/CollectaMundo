using CollectaMundo.Models;

namespace CollectaMundo.Domain
{
    namespace CollectaMundo.Domain
    {
        public interface ICardCollectionService
        {
            Task AddOrUpdateCardAsync(CardSet card);
            Task DeleteCardAsync(CardSet card);
            Task UpdateCardDetailsAsync(CardSet card);
        }
    }

}
