using CollectaMundo.DomainLogic.CardData.Models;

namespace CollectaMundo.DomainLogic.CardData
{
    public static class OracleFaceCanonicalizer
    {
        public static List<CanonicalOracleFace> Canonicalize(IReadOnlyList<OracleFaceCandidate> candidates)
        {
            return
            [
                .. candidates
                    .GroupBy(candidate => new OracleFaceKey(candidate.ScryfallOracleId, NormalizeSide(candidate.Side)))
                    .Select(CanonicalizeGroup)
                    .OrderBy(result => result.Key.ScryfallOracleId, StringComparer.Ordinal)
                    .ThenBy(result => result.Key.Side, StringComparer.Ordinal)
            ];
        }
        private static CanonicalOracleFace CanonicalizeGroup(IGrouping<OracleFaceKey, OracleFaceCandidate> group)
        {
            // GamePlayCard is canonical-face classification metadata, not part of the Oracle payload voting.
            //
            // All physical source rows representing the same Oracle face must agree on this classification.
            var gamePlayCardValues = group.Select(candidate => candidate.GamePlayCard).Distinct().ToArray();

            if (gamePlayCardValues.Length != 1)
            {
                throw new InvalidOperationException($"Oracle face '{group.Key.ScryfallOracleId}' / '{group.Key.Side}' contains conflicting GamePlayCard classifications: {string.Join(", ", gamePlayCardValues)}.");
            }

            var gamePlayCard = gamePlayCardValues[0];

            // Normalize representation before comparing Oracle payloads. Keyword ordering is not semantically significant.
            var variants = group
                .GroupBy(candidate => NormalizePayload(candidate.Payload))
                .Select(payloadGroup =>
                    new PayloadVariant(
                        Payload: payloadGroup.Key,
                        Count: payloadGroup.Count(),
                        SelectedSourceUuid: payloadGroup.Select(candidate => candidate.SourceUuid).OrderBy(uuid => uuid, StringComparer.Ordinal).First()))
                .OrderByDescending(variant => variant.Count)
                .ThenBy(variant => variant.SelectedSourceUuid, StringComparer.Ordinal)
                .ToList();

            var winner = variants[0];
            var isAmbiguous = variants.Count > 1 && variants[1].Count == winner.Count;

            return new CanonicalOracleFace(
                Key: group.Key,
                Payload: winner.Payload,
                GamePlayCard: gamePlayCard,
                SelectedSourceUuid: winner.SelectedSourceUuid,
                SourceRowCount: group.Count(),
                WinningRowCount: winner.Count,
                VariantCount: variants.Count,
                IsAmbiguous: isAmbiguous);
        }
        private static OracleFacePayload NormalizePayload(OracleFacePayload payload)
        {
            return payload with
            {
                Keywords = NormalizeKeywords(payload.Keywords)
            };
        }
        private static string NormalizeKeywords(string? keywords)
        {
            if (string.IsNullOrWhiteSpace(keywords))
            {
                return string.Empty;
            }

            return string.Join(", ", keywords.Split(',',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(keyword => keyword, StringComparer.OrdinalIgnoreCase));
        }
        private static string NormalizeSide(string? side)
        {
            return side ?? string.Empty;
        }
        private sealed record PayloadVariant(OracleFacePayload Payload, int Count, string SelectedSourceUuid);
    }
}

