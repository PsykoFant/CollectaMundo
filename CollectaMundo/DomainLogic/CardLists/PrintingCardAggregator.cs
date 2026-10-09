using CollectaMundo.DomainLogic.CardLegalities;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.DomainLogic.Shared.CardModels;

namespace CollectaMundo.DomainLogic.CardLists
{
    public static class PrintingCardAggregator
    {
        public static PrintingCardAggregationResult AggregatePrintingCards(IReadOnlyList<PrintingCard> printings)
        {
            var byUuid = new Dictionary<string, PrintingCard>(printings.Count, StringComparer.OrdinalIgnoreCase);
            var linkedUuids = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            var primaryCount = 0;

            for (var i = 0; i < printings.Count; i++)
            {
                var printing = printings[i];

                if (!string.IsNullOrWhiteSpace(printing.Uuid))
                {
                    byUuid.Add(printing.Uuid, printing);

                    foreach (var otherId in printing.Oracle.OtherFaceIds)
                    {
                        if (string.IsNullOrWhiteSpace(otherId))
                        {
                            continue;
                        }

                        AddLink(printing.Uuid, otherId);
                        AddLink(otherId, printing.Uuid);
                    }
                }

                if (IsPrimaryPrinting(printing))
                {
                    primaryCount++;
                }
            }

            void AddLink(string fromUuid, string toUuid)
            {
                if (!linkedUuids.TryGetValue(fromUuid, out var links))
                {
                    links = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    linkedUuids.Add(fromUuid, links);
                }

                links.Add(toUuid);
            }

            var results = new List<PrintingCard>(primaryCount);

            // Maps every physical/source UUID represented by an aggregate back to that aggregate.
            var aggregatedBySourceUuid = new Dictionary<string, PrintingCard>(printings.Count, StringComparer.OrdinalIgnoreCase);

            // Second pass:
            // aggregate only primary printings.
            for (var i = 0; i < printings.Count; i++)
            {
                var printing = printings[i];

                if (!IsPrimaryPrinting(printing))
                {
                    continue;
                }

                var oracle = printing.Oracle;

                // Fast path:
                // Most primary printings have nothing to aggregate.
                if (oracle.OtherFaceIds.Count == 0)
                {
                    var singleFaceAggregate = CreateAggregatedPrinting(
                            printing,
                            oracle.LegalityMasks,
                            CommaSeparatedValues.NormalizeAndDeduplicate(oracle.Keywords),
                            CommaSeparatedValues.NormalizeAndDeduplicate(oracle.Colors),
                            CommaSeparatedValues.NormalizeAndDeduplicate(oracle.Types),
                            NormalizeText(oracle.Text));

                    results.Add(singleFaceAggregate);

                    MapAggregateSourceUuids(printing.Uuid, singleFaceAggregate);

                    continue;
                }

                // Multi-face path.
                var allKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var allColors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var allTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var allTexts = new List<string>();
                ulong playableFormatsMask = 0;
                ulong restrictedFormatsMask = 0;

                MergeFrom(oracle);

                foreach (var otherId in oracle.OtherFaceIds)
                {
                    if (byUuid.TryGetValue(otherId, out var otherPrinting))
                    {
                        MergeFrom(otherPrinting.Oracle);
                    }
                }

                var aggregatedLegalityMasks = new CardLegalityMasks(PlayableFormatsMask: playableFormatsMask, RestrictedFormatsMask: restrictedFormatsMask);
                var multiFaceAggregate = CreateAggregatedPrinting(printing, aggregatedLegalityMasks, string.Join(",", allKeywords), string.Join(",", allColors), string.Join(",", allTypes), string.Join(" // ", allTexts));

                results.Add(multiFaceAggregate);

                // The primary UUID represents this aggregate.
                MapAggregateSourceUuids(printing.Uuid, multiFaceAggregate);

                void MapAggregateSourceUuids(string? primaryUuid, PrintingCard aggregate)
                {
                    if (string.IsNullOrWhiteSpace(primaryUuid))
                    {
                        return;
                    }

                    var pending = new Stack<string>();
                    var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    pending.Push(primaryUuid);

                    while (pending.Count > 0)
                    {
                        var sourceUuid = pending.Pop();

                        if (!visited.Add(sourceUuid))
                        {
                            continue;
                        }

                        // Only map UUIDs which actually correspond
                        // to source printings loaded by this aggregation.
                        if (byUuid.ContainsKey(sourceUuid))
                        {
                            AddSourceUuid(sourceUuid, aggregate);
                        }

                        if (!linkedUuids.TryGetValue(sourceUuid, out var linked))
                        {
                            continue;
                        }

                        foreach (var linkedUuid in linked)
                        {
                            pending.Push(linkedUuid);
                        }
                    }
                }

                void MergeFrom(OracleCard source)
                {
                    AddCsvValues(source.Keywords, allKeywords);
                    AddCsvValues(source.Colors, allColors);
                    AddCsvValues(source.Types, allTypes);
                    playableFormatsMask |= source.PlayableFormatsMask;
                    restrictedFormatsMask |= source.RestrictedFormatsMask;
                    if (!string.IsNullOrWhiteSpace(source.Text))
                    {
                        allTexts.Add(source.Text.Trim());
                    }
                }
            }

            return new PrintingCardAggregationResult(results, aggregatedBySourceUuid);

            void AddSourceUuid(string? sourceUuid, PrintingCard aggregatedPrinting)
            {
                if (string.IsNullOrWhiteSpace(sourceUuid))
                {
                    return;
                }

                if (aggregatedBySourceUuid.TryGetValue(sourceUuid, out var existing))
                {
                    if (!ReferenceEquals(existing, aggregatedPrinting))
                    {
                        throw new InvalidOperationException($"Printing UUID '{sourceUuid}' maps to more than one aggregated printing.");
                    }

                    return;
                }

                aggregatedBySourceUuid.Add(sourceUuid, aggregatedPrinting);
            }
        }
        private static bool IsPrimaryPrinting(PrintingCard printing)
        {
            var side = printing.Oracle.Side;

            return string.IsNullOrWhiteSpace(side) || side.Equals("a", StringComparison.OrdinalIgnoreCase);
        }
        private static PrintingCard CreateAggregatedPrinting(PrintingCard printing, CardLegalityMasks legalityMasks, string keywords, string colors, string types, string text)
        {
            var source = printing.Oracle;
            var aggregatedOracle = new OracleCard
            {
                ScryfallOracleId = source.ScryfallOracleId,
                Name = source.Name,
                ManaCost = source.ManaCost,
                ManaCostRaw = source.ManaCostRaw,
                Type = source.Type,
                Types = types,
                SuperTypes = source.SuperTypes,
                SubTypes = source.SubTypes,
                Side = source.Side,
                OtherFaceIds = source.OtherFaceIds,
                ManaValue = source.ManaValue,
                GamePlayCard = source.GamePlayCard,
                LegalityMasks = legalityMasks,
                Keywords = keywords,
                Colors = colors,
                Text = text
            };

            return new PrintingCard
            {
                Oracle = aggregatedOracle,
                LegalityMasks = legalityMasks,
                Uuid = printing.Uuid,
                SetCode = printing.SetCode,
                Language = printing.Language,
                Rarity = printing.Rarity,
                Finishes = printing.Finishes,
                Availability = printing.Availability,
                IsPromo = printing.IsPromo
            };
        }
        private static string NormalizeText(string? text)
        {
            return string.IsNullOrWhiteSpace(text)
                ? string.Empty
                : text.Trim();
        }
        private static void AddCsvValues(string? csv, HashSet<string> target)
        {
            if (string.IsNullOrWhiteSpace(csv))
            {
                return;
            }

            foreach (var value in csv.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = value.Trim();

                if (trimmed.Length > 0)
                {
                    target.Add(trimmed);
                }
            }
        }
    }
}
