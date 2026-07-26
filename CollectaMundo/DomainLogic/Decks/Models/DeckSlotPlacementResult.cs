using CollectaMundo.DomainLogic.Decks.Models.Enums;

namespace CollectaMundo.DomainLogic.Decks.Models
{
    public sealed class DeckSlotPlacementResult
    {
        public DeckSlotPlacementAction Action { get; init; }
        public bool IsAllowed => Action != DeckSlotPlacementAction.NotAllowed;
        public string? Message { get; init; }
    }
}
