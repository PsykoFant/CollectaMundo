namespace CollectaMundo.DomainLogic.CardLists.Models
{
    public sealed class CardCore
    {
        public required string Uuid { get; init; }
        public required string Name { get; init; }
        public List<string> OtherFaceIds { get; init; } = [];
        public string? SetCode { get; init; }
        public string? ManaCost { get; init; }
        public string? ManaCostRaw { get; init; }
        public string? Colors { get; init; }
        public string? SuperTypes { get; init; }
        public string? SubTypes { get; init; }
        public string? Type { get; init; }
        public string? Types { get; init; }
        public string? Keywords { get; init; }
        public string? Text { get; init; }
        public string? Side { get; init; }
        public string? Rarity { get; init; }
        public string? Finishes { get; init; }
        public double ManaValue { get; init; }
        public string? Language { get; init; }

        // Hydration Factory
        public static CardCore FromDto(CardCoreDto dto)
        {
            return new CardCore
            {
                Uuid = dto.Uuid ?? string.Empty,
                Name = dto.Name ?? string.Empty,
                ManaCostRaw = dto.ManaCostRaw,
                ManaCost = ProcessManaCost(dto.ManaCostRaw ?? ""),
                Colors = JoinAndDedup(dto.Colors),
                Type = JoinAndDedup(dto.Type),
                Types = JoinAndDedup(dto.Types),
                SuperTypes = JoinAndDedup(dto.SuperTypes),
                SubTypes = JoinAndDedup(dto.SubTypes),
                Keywords = JoinAndDedup(dto.Keywords),
                Text = dto.RulesText,
                Side = dto.Side,
                Language = dto.Language,
                OtherFaceIds = ParseOtherFaceIds(dto.OtherFaceIds),
                SetCode = dto.SetCode,
                Rarity = dto.Rarity,
                Finishes = dto.Finishes,
                ManaValue = dto.ManaValue ?? 0
            };
        }

        // Helpers
        private static string JoinAndDedup(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return string.Empty;

            return string.Join(",", csv
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private static List<string> ParseOtherFaceIds(string? raw)
        {
            return string.IsNullOrWhiteSpace(raw)
                ? []
                : [.. raw.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim())];
        }

        private static string ProcessManaCost(string raw)
        {
            char[] separators = ['{', '}'];
            return string.Join(",", raw.Split(separators, StringSplitOptions.RemoveEmptyEntries)).Trim(',');
        }
    }
}
