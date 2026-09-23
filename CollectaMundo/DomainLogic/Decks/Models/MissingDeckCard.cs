using CollectaMundo.DomainLogic.Shared.CardModels;

namespace CollectaMundo.DomainLogic.Decks.Models
{
    public sealed record MissingDeckCard
    {
        public required OracleCard Card { get; init; }

        public int DesiredQuantity { get; init; }
        public int AvailableQuantity { get; init; }
        public int MissingQuantity { get; init; }
    }
}
