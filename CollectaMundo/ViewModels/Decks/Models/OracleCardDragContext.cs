using CollectaMundo.DomainLogic.Decks.Models.Enums;
using CollectaMundo.DomainLogic.Shared.CardModels;

namespace CollectaMundo.ViewModels.Decks.Models
{
    public sealed class OracleCardDragContext
    {
        public required IReadOnlyList<OracleCard> Cards { get; init; }
        public bool IsOverValidTarget { get; set; }
        public DeckSection? DestinationSection { get; set; }
    }
}
