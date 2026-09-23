using CollectaMundo.DomainLogic.Decks.Models.Enums;

namespace CollectaMundo.DomainLogic.Decks.Models
{
    public sealed record DeckCardIdentityRecord(string OracleId, DeckSection Section);
}
