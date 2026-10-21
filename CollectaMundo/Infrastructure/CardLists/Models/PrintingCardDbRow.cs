namespace CollectaMundo.Infrastructure.CardLists.Models
{
    public sealed class PrintingCardDbRow
    {
        public string Uuid { get; init; } = string.Empty;
        public string ScryfallOracleId { get; init; } = string.Empty;
        public string Side { get; init; } = string.Empty;

        public string? SetCode { get; init; }
        public string? Language { get; init; }
        public string? Finishes { get; init; }
        public string? Availability { get; init; }
        public string? Rarity { get; init; }
        public bool IsPromo { get; init; }
        public string? OtherFaceIds { get; init; }
    }
}
