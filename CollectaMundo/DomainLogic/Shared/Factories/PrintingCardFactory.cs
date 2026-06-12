using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.Infrastructure.Shared.Models;
using System.Text;

namespace CollectaMundo.DomainLogic.Shared.Factories
{
    public static class PrintingCardFactory
    {
        public static PrintingCard FromRow(PrintingCardDbRow row)
        {
            var oracle = new OracleCard
            {
                ScryfallOracleId = row.ScryfallOracleId ?? string.Empty,
                Name = row.Name ?? string.Empty,
                ManaCostRaw = row.ManaCostRaw,
                ManaCost = ProcessManaCost(row.ManaCostRaw),
                Colors = JoinAndDedupCsv(row.Colors),
                Type = JoinAndDedupCsv(row.Type),
                Types = JoinAndDedupCsv(row.Types),
                SuperTypes = JoinAndDedupCsv(row.SuperTypes),
                SubTypes = JoinAndDedupCsv(row.SubTypes),
                Keywords = JoinAndDedupCsv(row.Keywords),
                Text = row.RulesText,
                Side = row.Side,
                OtherFaceIds = ParseOtherFaceIds(row.OtherFaceIds),
                ManaValue = row.ManaValue ?? 0
            };

            return new PrintingCard
            {
                Oracle = oracle,
                Uuid = row.Uuid ?? string.Empty,
                SetCode = row.SetCode,
                Language = row.Language,
                Rarity = row.Rarity,
                Finishes = row.Finishes
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

