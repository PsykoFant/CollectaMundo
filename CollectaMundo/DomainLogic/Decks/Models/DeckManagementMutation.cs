using CollectaMundo.ApplicationServices.Shared;

namespace CollectaMundo.DomainLogic.Decks.Models
{
    public sealed class DeckManagementMutation
    {
        public OperationResult Result { get; init; } = new(OperationResultCode.NoOp, "No operation was performed.");
        public DeckManagementRecord? Deck { get; init; }
    }
}
