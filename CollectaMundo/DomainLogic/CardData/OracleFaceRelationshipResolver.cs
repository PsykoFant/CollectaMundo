using CollectaMundo.DomainLogic.CardData.Models;

namespace CollectaMundo.DomainLogic.CardData
{
    public static class OracleFaceRelationshipResolver
    {
        public static OracleFaceRelationshipResolution Resolve(IReadOnlyList<OracleFaceCandidate> candidates)
        {
            ArgumentNullException.ThrowIfNull(candidates);

            // ---------------------------------------------------------
            // 1. Index physical source records by exact UUID
            // ---------------------------------------------------------

            var byUuid = new Dictionary<string, OracleFaceCandidate>(candidates.Count, StringComparer.OrdinalIgnoreCase);

            foreach (var candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate.SourceUuid))
                {
                    throw new InvalidOperationException("Oracle face candidate has a missing source UUID.");
                }

                if (string.IsNullOrWhiteSpace(candidate.ScryfallOracleId))
                {
                    throw new InvalidOperationException($"Source '{candidate.SourceUuid}' has no Oracle ID.");
                }

                if (!byUuid.TryAdd(candidate.SourceUuid, candidate))
                {
                    throw new InvalidOperationException($"Duplicate source UUID '{candidate.SourceUuid}'.");
                }
            }

            // ---------------------------------------------------------
            // 2. Identify standalone Oracle identities
            // ---------------------------------------------------------

            var standaloneOracleIds = candidates
                .Where(candidate => IsPrimarySide(candidate.Side))
                .Select(candidate => candidate.ScryfallOracleId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();

            var standaloneSet = standaloneOracleIds.ToHashSet(StringComparer.OrdinalIgnoreCase);

            // ---------------------------------------------------------
            // 3. Resolve cross-Oracle supporting faces
            // ---------------------------------------------------------

            var links = new HashSet<OracleCardFaceLink>();
            var missing = new HashSet<MissingOracleFaceReference>();

            foreach (var candidate in candidates)
            {
                // Only primary faces initiate relationships.
                if (!IsPrimarySide(candidate.Side))
                {
                    continue;
                }

                foreach (var referencedUuid in ParseOtherFaceIds(candidate.OtherFaceIds))
                {
                    if (!byUuid.TryGetValue(referencedUuid, out var related))
                    {
                        missing.Add(new MissingOracleFaceReference(candidate.SourceUuid, referencedUuid));

                        continue;
                    }

                    // Faces sharing an Oracle ID are already handled by ordinary canonical Oracle aggregation.
                    if (string.Equals(candidate.ScryfallOracleId, related.ScryfallOracleId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // A cross-Oracle relationship must point to a supporting face, never another primary face.
                    if (IsPrimarySide(related.Side))
                    {
                        throw new InvalidOperationException($"Source '{candidate.SourceUuid}' references primary face '{related.SourceUuid}' belonging to another Oracle ID.");
                    }

                    // Do not silently merge two independently selectable Oracle identities.
                    if (standaloneSet.Contains(related.ScryfallOracleId))
                    {
                        throw new InvalidOperationException($"Source '{candidate.SourceUuid}' references Oracle ID '{related.ScryfallOracleId}', which also has a standalone primary face.");
                    }

                    links.Add(new OracleCardFaceLink(PrimaryOracleId: candidate.ScryfallOracleId, SupportingFace: new OracleFaceKey(related.ScryfallOracleId, related.Side ?? string.Empty)));
                }
            }

            // ---------------------------------------------------------
            // 4. Return deterministic results
            // ---------------------------------------------------------

            var sortedLinks = links
                .OrderBy(link => link.PrimaryOracleId, StringComparer.Ordinal)
                .ThenBy(link => link.SupportingFace.ScryfallOracleId, StringComparer.Ordinal)
                .ThenBy(link => link.SupportingFace.Side, StringComparer.Ordinal)
                .ToArray();

            var sortedMissing = missing
                .OrderBy(item => item.SourceUuid, StringComparer.Ordinal)
                .ThenBy(item => item.ReferencedUuid, StringComparer.Ordinal)
                .ToArray();

            return new OracleFaceRelationshipResolution(StandaloneOracleIds: standaloneOracleIds, SupportingFaces: sortedLinks, MissingReferences: sortedMissing);
        }

        private static bool IsPrimarySide(string? side)
        {
            return string.IsNullOrWhiteSpace(side) || string.Equals(side, "a", StringComparison.OrdinalIgnoreCase);
        }

        private static IEnumerable<string> ParseOtherFaceIds(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? []
                : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }
}
