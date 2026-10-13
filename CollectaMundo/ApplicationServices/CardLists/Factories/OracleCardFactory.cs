using CollectaMundo.DomainLogic.CardLegalities;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.Infrastructure.CardLists.Models;

namespace CollectaMundo.ApplicationServices.CardLists.Factories
{
    public static class OracleCardFactory
    {
        private static readonly char[] ManaCostSeparators = ['{', '}'];
        public static OracleCard FromRow(CanonicalOracleFaceDbRow row, CardLegalityMasks legalityMasks = default)
        {
            return new OracleCard
            {
                ScryfallOracleId = row.ScryfallOracleId,
                Side = row.Side,

                Name = row.Name ?? string.Empty,

                GamePlayCard = row.GamePlayCard,
                ManaCostRaw = row.ManaCostRaw,
                ManaCost = ProcessManaCost(row.ManaCostRaw),
                ManaValue = row.ManaValue ?? 0,
                Colors = CommaSeparatedValues.NormalizeAndDeduplicate(row.Colors),
                Keywords = CommaSeparatedValues.NormalizeAndDeduplicate(row.Keywords),
                SuperTypes = CommaSeparatedValues.NormalizeAndDeduplicate(row.SuperTypes),
                Types = CommaSeparatedValues.NormalizeAndDeduplicate(row.Types),
                SubTypes = CommaSeparatedValues.NormalizeAndDeduplicate(row.SubTypes),
                Type = CommaSeparatedValues.NormalizeAndDeduplicate(row.Type),
                Text = string.IsNullOrWhiteSpace(row.RulesText) ? string.Empty : row.RulesText.Trim(),
                LegalityMasks = new CardLegalityMasks(PlayableFormatsMask: row.PlayableFormatsMask, RestrictedFormatsMask: row.RestrictedFormatsMask)
            };
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
