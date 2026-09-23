using CollectaMundo.DomainLogic.Decks.Models;

namespace CollectaMundo.ApplicationServices.Decks
{
    public interface IDeckCardReader
    {
        Task<IReadOnlyList<DeckCardEntry>> LoadAsync(int deckLocationId);
    }
}
