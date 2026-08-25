using CollectaMundo.ViewModels.Decks.Models.RowViewModels;

namespace CollectaMundo.ViewModels.Decks.Models.DragMoveViewRequests
{
    public sealed record DeckCardDragItem(DeckCardEntryViewModel Card, int Quantity);
}
