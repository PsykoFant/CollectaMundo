
using CollectaMundo.DomainLogic.CardData.Models;

namespace CollectaMundo.DomainLogic.CardData
{
    public static class OracleCardAggregator
    {
        public static List<AggregatedOracleCard> Aggregate(IReadOnlyList<CanonicalOracleFace> canonicalFaces, OracleFaceRelationshipResolution relationships)
        {
            ArgumentNullException.ThrowIfNull(canonicalFaces);
            ArgumentNullException.ThrowIfNull(relationships);

            // ---------------------------------------------------------
            // 1. Index canonical faces by Oracle ID and face identity
            // ---------------------------------------------------------

            var facesByOracleId = canonicalFaces.GroupBy(face => face.Key.ScryfallOracleId, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

            var facesByKey = canonicalFaces.ToDictionary(face => face.Key);

            // ---------------------------------------------------------
            // 2. Index cross-Oracle supporting-face relationships
            // ---------------------------------------------------------

            var supportingFacesByOracleId = relationships.SupportingFaces
                .GroupBy(link => link.PrimaryOracleId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(link => link.SupportingFace)
                        .Distinct()
                        .OrderBy(key => key.ScryfallOracleId, StringComparer.Ordinal)
                        .ThenBy(key => key.Side, StringComparer.Ordinal)
                        .ToList(),
                    StringComparer.Ordinal);

            var standaloneIds = relationships.StandaloneOracleIds.ToHashSet(StringComparer.Ordinal);

            // Every relationship must belong to a standalone Oracle card.
            foreach (var primaryId in supportingFacesByOracleId.Keys)
            {
                if (!standaloneIds.Contains(primaryId))
                {
                    throw new InvalidOperationException($"Supporting-face relationship references non-standalone Oracle card '{primaryId}'.");
                }
            }

            // ---------------------------------------------------------
            // 3. Aggregate standalone Oracle cards
            // ---------------------------------------------------------

            var result = new List<AggregatedOracleCard>(standaloneIds.Count);

            foreach (var oracleId in standaloneIds.OrderBy(id => id, StringComparer.Ordinal))
            {
                if (!facesByOracleId.TryGetValue(oracleId, out var primaryFaces))
                {
                    throw new InvalidOperationException($"Standalone Oracle card '{oracleId}' has no canonical faces.");
                }

                // Aggregate faces sharing the primary Oracle identity. This handles split, Aftermath, Adventure, transform, etc.
                var primary = AggregateGroup(oracleId, primaryFaces);

                // No cross-Oracle supporting faces: the primary aggregation is already complete.
                if (!supportingFacesByOracleId.TryGetValue(oracleId, out var supportingKeys))
                {
                    result.Add(primary);
                    continue;
                }

                // -----------------------------------------------------
                // 4. Resolve canonical supporting faces
                // -----------------------------------------------------

                var orderedPrimaryFaces = OrderFaces(primaryFaces);

                var allFaces = new List<CanonicalOracleFace>(orderedPrimaryFaces.Count + supportingKeys.Count);

                allFaces.AddRange(orderedPrimaryFaces);

                foreach (var key in supportingKeys)
                {
                    if (!facesByKey.TryGetValue(key, out var supportingFace))
                    {
                        throw new InvalidOperationException($"Oracle card '{oracleId}' references supporting face '{key.ScryfallOracleId}' / '{key.Side}', but no canonical face exists.");
                    }

                    allFaces.Add(supportingFace);
                }

                // -----------------------------------------------------
                // 5. Merge supporting-face searchable properties
                // -----------------------------------------------------

                // Preserve the primary's name, mana properties, type line, gameplay classification, supertypes and subtypes.
                //
                // Merge only the properties previously combined by PrintingCardAggregator for cross-Oracle relationships.

                var aggregated = primary with
                {
                    Colors = UnionCsv(allFaces, p => p.Colors),
                    Keywords = UnionCsv(allFaces, p => p.Keywords),
                    Types = UnionCsv(allFaces, p => p.Types),
                    RulesText = CombineText(allFaces)
                };

                result.Add(aggregated);
            }

            return result;
        }

        // ---------------------------------------------------------
        // Aggregate canonical faces belonging to one Oracle identity
        // ---------------------------------------------------------

        private static AggregatedOracleCard AggregateGroup(string oracleId, IReadOnlyList<CanonicalOracleFace> faces)
        {
            var orderedFaces = OrderFaces(faces);

            ValidateGroup(oracleId, orderedFaces);

            var front = orderedFaces[0].Payload;

            return new AggregatedOracleCard(
                ScryfallOracleId: oracleId,
                Name: front.Name!,
                GamePlayCard: orderedFaces[0].GamePlayCard,

                // Primary/front-face scalar properties
                ManaCostRaw: front.ManaCostRaw,
                ManaValue: front.ManaValue,
                Type: front.Type,

                // Properties combined across faces sharing this Oracle ID
                Colors: UnionCsv(orderedFaces, p => p.Colors),
                Keywords: UnionCsv(orderedFaces, p => p.Keywords),
                SuperTypes: UnionCsv(orderedFaces, p => p.SuperTypes),
                Types: UnionCsv(orderedFaces, p => p.Types),
                SubTypes: UnionCsv(orderedFaces, p => p.SubTypes),
                RulesText: CombineText(orderedFaces));
        }
        private static List<CanonicalOracleFace> OrderFaces(IEnumerable<CanonicalOracleFace> faces)
        {
            return [.. faces.OrderBy(face => SidePriority(face.Key.Side)).ThenBy(face => face.Key.Side, StringComparer.Ordinal)];
        }
        private static int SidePriority(string? side)
        {
            if (string.Equals(side, "a", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (string.IsNullOrWhiteSpace(side))
            {
                return 1;
            }

            return 2;
        }

        // ---------------------------------------------------------
        // Validate canonical faces sharing one Oracle identity
        // ---------------------------------------------------------

        private static void ValidateGroup(string oracleId, IReadOnlyList<CanonicalOracleFace> faces)
        {
            if (faces.Any(face => string.IsNullOrWhiteSpace(face.Payload.Name)))
            {
                throw new InvalidOperationException($"Oracle card '{oracleId}' has a missing name.");
            }

            var names = faces.Select(face => face.Payload.Name!).Distinct(StringComparer.Ordinal).ToList();

            if (names.Count != 1)
            {
                throw new InvalidOperationException($"Oracle card '{oracleId}' has conflicting names: " + string.Join(" | ", names));
            }

            var classifications = faces.Select(face => face.GamePlayCard).Distinct().ToList();

            if (classifications.Count != 1)
            {
                throw new InvalidOperationException($"Oracle card '{oracleId}' has conflicting  GamePlayCard classifications.");
            }

            var duplicateSides = faces
                .GroupBy(face => face.Key.Side, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();

            if (duplicateSides.Count > 0)
            {
                throw new InvalidOperationException($"Oracle card '{oracleId}' has duplicate sides: " + string.Join(", ", duplicateSides));
            }
        }

        // ---------------------------------------------------------
        // Shared aggregation helpers
        // ---------------------------------------------------------

        private static string UnionCsv(IEnumerable<CanonicalOracleFace> faces, Func<OracleFacePayload, string?> selector)
        {
            return string.Join(",",
                faces
                    .SelectMany(face =>
                        (selector(face.Payload) ?? string.Empty)
                            .Split(
                                ',',
                                StringSplitOptions.RemoveEmptyEntries |
                                StringSplitOptions.TrimEntries))
                    .Where(value => value.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(value => value, StringComparer.Ordinal));
        }
        private static string CombineText(IEnumerable<CanonicalOracleFace> faces)
        {
            return string.Join(
                " // ",
                faces.Select(face => face.Payload.RulesText?.Trim()).Where(text => !string.IsNullOrWhiteSpace(text)));
        }
    }
}
