namespace CollectaMundo.DomainLogic.CardData.Models
{
    public sealed record OracleFaceRelationshipResolution(IReadOnlyList<string> StandaloneOracleIds, IReadOnlyList<OracleCardFaceLink> SupportingFaces, IReadOnlyList<MissingOracleFaceReference> MissingReferences);
}
