using CollectaMundo.DomainLogic.Decks.Models.Enums;
using CollectaMundo.Presentation.Behaviors;

namespace CollectaMundo.ViewModels.Decks.Models
{
    public sealed record DeckCardDragRequest(IReadOnlyList<DeckCardDragItem> Items, DeckSection? DestinationSection);
}
