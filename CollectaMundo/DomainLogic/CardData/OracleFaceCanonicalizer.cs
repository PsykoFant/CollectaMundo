using CollectaMundo.DomainLogic.CardData.Models;

namespace CollectaMundo.DomainLogic.CardData
{
    public static class OracleFaceCanonicalizer
    {
        public static List<CanonicalOracleFace> Canonicalize(IReadOnlyList<OracleFaceCandidate> candidates)
        {
            return
            [
                .. candidates.GroupBy(candidate => new OracleFaceKey(
                    candidate.ScryfallOracleId,
                    NormalizeSide(candidate.Side))).Select(CanonicalizeGroup).OrderBy(result => result.Key.ScryfallOracleId,StringComparer.Ordinal)
                    .ThenBy(result => result.Key.Side,StringComparer.Ordinal)
            ];
        }
        private static CanonicalOracleFace CanonicalizeGroup(IGrouping<OracleFaceKey, OracleFaceCandidate> group)
        {
            var variants = group
                .GroupBy(candidate => candidate.Payload)
                .Select(payloadGroup => new PayloadVariant(Payload: payloadGroup.Key, Count: payloadGroup.Count(), SelectedSourceUuid: payloadGroup
                        .Select(candidate => candidate.SourceUuid)
                        .OrderBy(uuid => uuid, StringComparer.Ordinal).First()))
                .OrderByDescending(variant => variant.Count)
                .ThenBy(variant => variant.SelectedSourceUuid, StringComparer.Ordinal)
                .ToList();

            var winner = variants[0];
            var isAmbiguous = variants.Count > 1 && variants[1].Count == winner.Count;

            return new CanonicalOracleFace(
                Key: group.Key,
                Payload: winner.Payload,
                SelectedSourceUuid: winner.SelectedSourceUuid,
                SourceRowCount: group.Count(),
                WinningRowCount: winner.Count,
                VariantCount: variants.Count,
                IsAmbiguous: isAmbiguous);
        }
        private static string NormalizeSide(string? side)
        {
            return side ?? string.Empty;
        }
        private sealed record PayloadVariant(OracleFacePayload Payload, int Count, string SelectedSourceUuid);
    }
}
