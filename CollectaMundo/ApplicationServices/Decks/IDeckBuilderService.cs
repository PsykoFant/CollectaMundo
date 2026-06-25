using CollectaMundo.DomainLogic.Decks.Models;

namespace CollectaMundo.ApplicationServices.Decks
{
    public interface IDeckBuilderService
    {
        public Task<IReadOnlyList<DeckCardEntry>> LoadDeckAsync(int locationId);
        Task SaveDeckAsync(int locationId, IEnumerable<DeckCardEntry> entries);
    }
}
