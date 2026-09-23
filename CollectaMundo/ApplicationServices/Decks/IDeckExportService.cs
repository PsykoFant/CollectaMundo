using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.DomainLogic.Shared.CollectionSnapshot;

namespace CollectaMundo.ApplicationServices.Decks
{
    public interface IDeckExportService
    {
        Task<IReadOnlyList<DeckCardState>> GetCompleteDeckAsync(int deckLocationId);
        Task<IReadOnlyList<MissingDeckCard>> GetMissingCardsAsync(int deckLocationId, ICollectionQuantitySnapshot collectionSnapshot);
    }
}
