using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.DomainLogic.Shared.CollectionSnapshot;

namespace CollectaMundo.ApplicationServices.Decks.DeckExport
{
    public interface IDeckExportService
    {
        Task<IReadOnlyList<DeckCardState>> GetCompleteDeckAsync(int deckLocationId, IReadOnlyList<OracleCard> oracleCards);
        Task<IReadOnlyList<MissingDeckCard>> GetMissingCardsAsync(int deckLocationId, IReadOnlyList<OracleCard> oracleCards, ICollectionQuantitySnapshot quantitySnapshot);
        string Format(IReadOnlyList<MissingDeckCard> cards);
    }
}
