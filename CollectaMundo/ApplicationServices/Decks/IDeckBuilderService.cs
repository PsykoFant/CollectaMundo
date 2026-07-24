using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.DomainLogic.Shared.CardModels;

namespace CollectaMundo.ApplicationServices.Decks
{
    public interface IDeckBuilderService
    {
        public Task<IReadOnlyList<DeckCardEntry>> LoadDeckAsync(int locationId);
        Task SaveDeckAsync(int locationId, IReadOnlyCollection<DeckCardState> cards);
        Task SaveDeckAsync(int locationId, IEnumerable<DeckCardEntry> entries);
        DeckActionAvailability GetActionAvailability(string? format, IReadOnlyCollection<DeckCardState> deckCards, OracleCard selectedCard);
        Task<SetCommanderResult> SetCommanderAsync(int deckLocationId, string? format, IReadOnlyCollection<DeckCardState> currentCards, OracleCard selectedCard);
    }
}
