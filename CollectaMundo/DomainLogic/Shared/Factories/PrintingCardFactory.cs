using CollectaMundo.DomainLogic.CardLegalities;
using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.Infrastructure.CardLists.Models;

namespace CollectaMundo.DomainLogic.Shared.Factories
{
    public static class PrintingCardFactory
    {
        private static readonly char[] ManaCostSeparators = ['{', '}'];
        public static PrintingCard FromRow(PrintingCardDbRow row, CardLegalityMasks legalityMasks = default)
        {
            var oracle = new OracleCard
            {
                Colors = CommaSeparatedValues.NormalizeAndDeduplicate(row.Colors),
                GamePlayCard = row.GamePlayCard,
                Keywords = CommaSeparatedValues.NormalizeAndDeduplicate(row.Keywords),
                LegalityMasks = legalityMasks,
                ManaCost = ProcessManaCost(row.ManaCostRaw),
                ManaCostRaw = row.ManaCostRaw,
                ManaValue = row.ManaValue ?? 0,
                Name = row.Name ?? string.Empty,
                OtherFaceIds = ParseOtherFaceIds(row.OtherFaceIds),
                ScryfallOracleId = row.ScryfallOracleId ?? string.Empty,
                Side = row.Side,
                SubTypes = CommaSeparatedValues.NormalizeAndDeduplicate(row.SubTypes),
                SuperTypes = CommaSeparatedValues.NormalizeAndDeduplicate(row.SuperTypes),
                Text = row.RulesText,
                Type = CommaSeparatedValues.NormalizeAndDeduplicate(row.Type),
                Types = CommaSeparatedValues.NormalizeAndDeduplicate(row.Types)
            };

            return new PrintingCard
            {
                Availability = row.Availability,
                Finishes = row.Finishes,
                IsPromo = row.IsPromo,
                Language = row.Language,
                LegalityMasks = legalityMasks,
                Oracle = oracle,
                Rarity = row.Rarity,
                SetCode = row.SetCode,
                Uuid = row.Uuid ?? string.Empty
            };
        }
        private static IReadOnlyList<string> ParseOtherFaceIds(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return [];
            }

            var values = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return values.Length == 0
                ? []
                : values;
        }
        private static string ProcessManaCost(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            return string.Join(",", raw.Split(ManaCostSeparators, StringSplitOptions.RemoveEmptyEntries)).Trim(',');
        }
    }
}

