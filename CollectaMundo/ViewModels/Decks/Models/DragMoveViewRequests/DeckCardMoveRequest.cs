using CollectaMundo.DomainLogic.Decks.Models.Enums;
using CollectaMundo.DomainLogic.Shared.CardModels;

namespace CollectaMundo.ViewModels.Decks.Models.DragMoveViewRequests

{
    public sealed record DeckCardMoveRequest(OracleCard Card, DeckSection SourceSection, int Quantity);
}
