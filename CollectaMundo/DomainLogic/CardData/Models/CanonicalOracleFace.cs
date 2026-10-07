namespace CollectaMundo.DomainLogic.CardData.Models
{
    public sealed record CanonicalOracleFace(OracleFaceKey Key, OracleFacePayload Payload, string SelectedSourceUuid, int SourceRowCount, int WinningRowCount, int VariantCount, bool IsAmbiguous)
    {
        public bool HasConflict => VariantCount > 1;
    }
}
