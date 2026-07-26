using CollectaMundo.DomainLogic.Decks.Models.Enums;

namespace CollectaMundo.DomainLogic.Decks.Models.Records
{
    public sealed record DeckCardIdentityRecord(string OracleId, DeckSection Section);
}
