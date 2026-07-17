using CollectaMundo.DomainLogic.CardLegalities;
using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.Infrastructure.Shared.Models;
using System.Text;

namespace CollectaMundo.DomainLogic.Shared.Factories
{
    public static class PrintingCardFactory
    {
        public static PrintingCard FromRow(PrintingCardDbRow row, CardLegalityMasks legalityMasks = default)
        {
            var oracle = new OracleCard
            {
                Colors = JoinAndDedupCsv(row.Colors),
                GamePlayCard = row.GamePlayCard,
                Keywords = JoinAndDedupCsv(row.Keywords),
                LegalityMasks = legalityMasks,
                ManaCost = ProcessManaCost(row.ManaCostRaw),
                ManaCostRaw = row.ManaCostRaw,
                ManaValue = row.ManaValue ?? 0,
                Name = row.Name ?? string.Empty,
                OtherFaceIds = ParseOtherFaceIds(row.OtherFaceIds),
                ScryfallOracleId = row.ScryfallOracleId ?? string.Empty,
                Side = row.Side,
                SubTypes = JoinAndDedupCsv(row.SubTypes),
                SuperTypes = JoinAndDedupCsv(row.SuperTypes),
                Text = row.RulesText,
                Type = JoinAndDedupCsv(row.Type),
                Types = JoinAndDedupCsv(row.Types)
            };

            return new PrintingCard
            {
                Availability = row.Availability,
                Finishes = row.Finishes,
                Language = row.Language,
                LegalityMasks = legalityMasks,
                Oracle = oracle,
                Rarity = row.Rarity,
                SetCode = row.SetCode,
                Uuid = row.Uuid ?? string.Empty
            };
        }

        private static string JoinAndDedupCsv(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv))
            {
                return string.Empty;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var segments = csv.Split(',');

            var sb = new StringBuilder();

            foreach (var segment in segments)
            {
                var trimmed = segment.Trim();

                if (trimmed.Length == 0 || !seen.Add(trimmed))
                {
                    continue;
                }

                if (sb.Length > 0)
                {
                    sb.Append(',');
                }

                sb.Append(trimmed);
            }

            return sb.ToString();
        }
        private static List<string> ParseOtherFaceIds(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return [];
            }

            return [.. raw
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)];
        }
        private static string ProcessManaCost(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            char[] separators = ['{', '}'];

            return string
                .Join(",", raw.Split(separators, StringSplitOptions.RemoveEmptyEntries))
                .Trim(',');
        }
    }
}

