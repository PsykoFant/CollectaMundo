namespace CollectaMundo.Infrastructure.Shared.Models
{
    public sealed class PrintingCardDbRow
    {
        public string? ScryfallOracleId { get; init; }

        public string? Name { get; init; }
        public string? ManaCostRaw { get; init; }
        public string? Colors { get; init; }
        public string? Type { get; init; }
        public string? Types { get; init; }
        public string? SuperTypes { get; init; }
        public string? SubTypes { get; init; }
        public string? Keywords { get; init; }
        public string? RulesText { get; init; }
        public string? Side { get; init; }
        public string? OtherFaceIds { get; init; }
        public double? ManaValue { get; init; }

        public string? Uuid { get; init; }
        public string? Language { get; init; }
        public string? SetCode { get; init; }
        public string? Rarity { get; init; }
        public string? Finishes { get; init; }
    }
}
