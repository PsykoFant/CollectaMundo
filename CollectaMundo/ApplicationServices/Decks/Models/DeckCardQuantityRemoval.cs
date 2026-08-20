using CollectaMundo.DomainLogic.Decks.Models.Enums;
using CollectaMundo.DomainLogic.Shared.CardModels;

namespace CollectaMundo.ApplicationServices.Decks.Models
{
    public sealed record DeckCardQuantityRemoval(OracleCard Card, DeckSection Section, int Quantity);
}
