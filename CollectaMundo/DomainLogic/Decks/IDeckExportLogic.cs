using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.DomainLogic.Shared.CollectionSnapshot;

namespace CollectaMundo.DomainLogic.Decks
{
    public interface IDeckExportLogic
    {
        IReadOnlyList<DeckCardState> GetCompleteDeck(IEnumerable<DeckCardState> cards);
        IReadOnlyList<MissingDeckCard> GetMissingCards(IEnumerable<DeckCardState> cards, ICollectionQuantitySnapshot quantitySnapshot, int deckLocationId);
    }
}
