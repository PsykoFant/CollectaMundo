using CollectaMundo.DomainLogic.CardData.Models;

namespace CollectaMundo.DomainLogic.CardData
{
    public static class OracleCardAggregator
    {
        public static List<AggregatedOracleCard> Aggregate(IReadOnlyList<CanonicalOracleFace> faces)
        {
            ArgumentNullException.ThrowIfNull(faces);

            return [.. faces.GroupBy(face => face.Key.ScryfallOracleId, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(AggregateGroup)];
        }
        private static AggregatedOracleCard AggregateGroup(IGrouping<string, CanonicalOracleFace> group)
        {
            var orderedFaces = group.OrderBy(face => SidePriority(face.Key.Side))
                .ThenBy(face => face.Key.Side, StringComparer.Ordinal)
                .ToList();

            ValidateGroup(group.Key, orderedFaces);

            var front = orderedFaces[0].Payload;

            return new AggregatedOracleCard(ScryfallOracleId: group.Key, Name: front.Name!, GamePlayCard: orderedFaces[0].GamePlayCard,

                // Front-face properties
                ManaCostRaw: front.ManaCostRaw,
                ManaValue: front.ManaValue,
                Type: front.Type,

                // Aggregated properties
                Colors: UnionCsv(orderedFaces, p => p.Colors),
                Keywords: UnionCsv(orderedFaces, p => p.Keywords),
                SuperTypes: UnionCsv(orderedFaces, p => p.SuperTypes),
                Types: UnionCsv(orderedFaces, p => p.Types),
                SubTypes: UnionCsv(orderedFaces, p => p.SubTypes),
                RulesText: CombineText(orderedFaces));
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
                throw new InvalidOperationException($"Oracle card '{oracleId}' has conflicting GamePlayCard classifications.");
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
        private static string UnionCsv(IEnumerable<CanonicalOracleFace> faces, Func<OracleFacePayload, string?> selector)
        {
            return string.Join(",",
                faces
                    .SelectMany(face =>
                        (selector(face.Payload) ?? string.Empty)
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    .Where(value => value.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(value => value, StringComparer.Ordinal));
        }
        private static string CombineText(IEnumerable<CanonicalOracleFace> faces)
        {
            return string.Join(" // ", faces.Select(face => face.Payload.RulesText?.Trim()).Where(text => !string.IsNullOrWhiteSpace(text)));
        }
    }
}
