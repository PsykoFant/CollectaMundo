using CollectaMundo.DomainLogic.Decks.Models;

namespace CollectaMundo.ApplicationServices.Decks.Shared
{
    public interface IDeckCardReader
    {
        Task<IReadOnlyList<DeckCardEntry>> LoadAsync(int deckLocationId);
    }
}
