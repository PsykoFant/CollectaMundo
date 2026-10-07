namespace CollectaMundo.DomainLogic.CardData.Models
{
    public sealed record OracleFaceCandidate(string SourceUuid, string ScryfallOracleId, string? Side, OracleFacePayload Payload);
}
