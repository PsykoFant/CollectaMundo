using CollectaMundo.DomainLogic.Decks.Models.Enums;

namespace CollectaMundo.ViewModels.Decks.Models.DragMoveViewRequests
{
    public sealed record DeckCardDragRequest(IReadOnlyList<DeckCardDragItem> Items, DeckSection? DestinationSection);
}
