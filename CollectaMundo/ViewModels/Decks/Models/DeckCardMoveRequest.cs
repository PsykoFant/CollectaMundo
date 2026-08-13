using CollectaMundo.DomainLogic.Decks.Models.Enums;

namespace CollectaMundo.ViewModels.Decks.Models
{
    public sealed record DeckCardMoveRequest(DeckCardEntryViewModel Card, DeckSection DestinationSection);
}
