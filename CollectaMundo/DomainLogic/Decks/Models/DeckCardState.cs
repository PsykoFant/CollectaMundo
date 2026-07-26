using CollectaMundo.DomainLogic.Decks.Models.Enums;
using CollectaMundo.DomainLogic.Shared.CardModels;

namespace CollectaMundo.DomainLogic.Decks.Models
{
    public sealed class DeckCardState
    {
        public required OracleCard Card { get; init; }
        public int DesiredQuantity { get; init; }
        public DeckSection Section { get; init; }
    }
}
