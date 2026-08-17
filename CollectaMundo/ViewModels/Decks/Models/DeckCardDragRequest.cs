using CollectaMundo.DomainLogic.Decks.Models.Enums;

namespace CollectaMundo.ViewModels.Decks.Models
{
    public sealed record DeckCardDragRequest(DeckCardEntryViewModel Card, DeckSection? DestinationSection, int Quantity);
}
