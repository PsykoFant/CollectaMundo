namespace CollectaMundo.Infrastructure.CardLists.Models
{
    public sealed class CanonicalOracleFaceDbRow
    {
        public string ScryfallOracleId { get; init; } = string.Empty;
        public string Side { get; init; } = string.Empty;
        public string SelectedSourceUuid { get; init; } = string.Empty;

        public int GamePlayCard { get; init; }
        public string? Name { get; init; }
        public string? ManaCostRaw { get; init; }
        public double? ManaValue { get; init; }
        public string? Colors { get; init; }
        public string? Keywords { get; init; }
        public string? RulesText { get; init; }
        public string? SuperTypes { get; init; }
        public string? Types { get; init; }
        public string? SubTypes { get; init; }
        public string? Type { get; init; }
    }
}
