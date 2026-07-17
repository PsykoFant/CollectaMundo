using CollectaMundo.DomainLogic.CardLegalities;
using CollectaMundo.DomainLogic.Shared.CardModels;

namespace CollectaMundo.DomainLogic.CardLists
{
    public static class PrintingCardAggregator
    {
        public static List<PrintingCard> Aggregate(IEnumerable<PrintingCard> printings)
        {
            var printingList = printings.ToList();

            var byUuid = printingList.Where(p => !string.IsNullOrWhiteSpace(p.Uuid)).ToDictionary(p => p.Uuid!, StringComparer.OrdinalIgnoreCase);
            var primaryPrintings = printingList.Where(p => string.IsNullOrWhiteSpace(p.Oracle.Side) || p.Oracle.Side.Equals("a", StringComparison.OrdinalIgnoreCase)).ToList();
            var results = new List<PrintingCard>(primaryPrintings.Count);

            foreach (var printing in primaryPrintings)
            {
                var allKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var allColors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var allTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var allTexts = new List<string>();

                ulong playableFormatsMask = 0;
                ulong restrictedFormatsMask = 0;

                MergeFrom(printing.Oracle);

                foreach (var otherId in printing.Oracle.OtherFaceIds)
                {
                    if (byUuid.TryGetValue(otherId, out var otherPrinting))
                    {
                        MergeFrom(otherPrinting.Oracle);
                    }
                }

                var aggregatedLegalityMasks = new CardLegalityMasks(PlayableFormatsMask: playableFormatsMask, RestrictedFormatsMask: restrictedFormatsMask);

                void MergeFrom(OracleCard oracle)
                {
                    AddCsvValues(oracle.Keywords, allKeywords);
                    AddCsvValues(oracle.Colors, allColors);
                    AddCsvValues(oracle.Types, allTypes);

                    playableFormatsMask |= oracle.PlayableFormatsMask;
                    restrictedFormatsMask |= oracle.RestrictedFormatsMask;

                    if (!string.IsNullOrWhiteSpace(oracle.Text))
                    {
                        allTexts.Add(oracle.Text.Trim());
                    }
                }

                var aggregatedOracle = new OracleCard
                {
                    ScryfallOracleId = printing.Oracle.ScryfallOracleId,
                    Name = printing.Oracle.Name,
                    ManaCost = printing.Oracle.ManaCost,
                    ManaCostRaw = printing.Oracle.ManaCostRaw,
                    Type = printing.Oracle.Type,
                    Types = string.Join(",", allTypes),
                    SuperTypes = printing.Oracle.SuperTypes,
                    SubTypes = printing.Oracle.SubTypes,
                    Side = printing.Oracle.Side,
                    OtherFaceIds = printing.Oracle.OtherFaceIds,
                    ManaValue = printing.Oracle.ManaValue,

                    GamePlayCard = printing.Oracle.GamePlayCard,

                    LegalityMasks = aggregatedLegalityMasks,

                    Keywords = string.Join(",", allKeywords),
                    Colors = string.Join(",", allColors),
                    Text = string.Join(" // ", allTexts)
                };

                results.Add(new PrintingCard
                {
                    Oracle = aggregatedOracle,

                    LegalityMasks = aggregatedLegalityMasks,

                    Uuid = printing.Uuid,
                    SetCode = printing.SetCode,
                    Language = printing.Language,
                    Rarity = printing.Rarity,
                    Finishes = printing.Finishes,
                    Availability = printing.Availability
                });
            }

            return results;
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
