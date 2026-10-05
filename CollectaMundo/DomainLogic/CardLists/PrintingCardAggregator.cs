using CollectaMundo.DomainLogic.CardLegalities;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.DomainLogic.Shared.CardModels;

namespace CollectaMundo.DomainLogic.CardLists
{
    public static class PrintingCardAggregator
    {
        public static List<PrintingCard> AggregatePrintingCards(IReadOnlyList<PrintingCard> printings)
        {
            var byUuid = new Dictionary<string, PrintingCard>(printings.Count, StringComparer.OrdinalIgnoreCase);
            var primaryCount = 0;

            // First pass:
            // - build UUID lookup
            // - determine exact result capacity
            for (var i = 0; i < printings.Count; i++)
            {
                var printing = printings[i];

                if (!string.IsNullOrWhiteSpace(printing.Uuid))
                {
                    byUuid.Add(printing.Uuid, printing);
                }

                if (IsPrimaryPrinting(printing))
                {
                    primaryCount++;
                }
            }

            var results = new List<PrintingCard>(primaryCount);

            // Second pass:
            // aggregate only primary printings
            for (var i = 0; i < printings.Count; i++)
            {
                var printing = printings[i];

                if (!IsPrimaryPrinting(printing))
                {
                    continue;
                }

                var oracle = printing.Oracle;

                // Fast path:
                // ~95.5% of primary printings have nothing to aggregate.
                if (oracle.OtherFaceIds.Count == 0)
                {
                    results.Add(CreateAggregatedPrinting(
                        printing,
                        oracle.LegalityMasks,
                        CommaSeparatedValues.NormalizeAndDeduplicate(oracle.Keywords),
                        CommaSeparatedValues.NormalizeAndDeduplicate(oracle.Colors),
                        CommaSeparatedValues.NormalizeAndDeduplicate(oracle.Types),
                        NormalizeText(oracle.Text)));

                    continue;
                }

                // Multi-face path:
                // only ~4.5% of primary printings need full aggregation.
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

                results.Add(CreateAggregatedPrinting(printing, aggregatedLegalityMasks, string.Join(",", allKeywords), string.Join(",", allColors), string.Join(",", allTypes), string.Join(" // ", allTexts)));

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

            return results;
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
