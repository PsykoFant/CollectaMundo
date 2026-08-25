using CollectaMundo.DomainLogic.Decks.Models.Enums;
using CollectaMundo.DomainLogic.Shared.CardModels;

namespace CollectaMundo.ViewModels.Decks.Models.DragMoveViewRequests
{
    public sealed record DeckOracleCardDropRequest(IReadOnlyList<OracleCard> Cards, DeckSection DestinationSection, int Quantity);
}
