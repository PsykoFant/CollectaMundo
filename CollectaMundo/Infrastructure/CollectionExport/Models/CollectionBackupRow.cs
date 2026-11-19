namespace CollectaMundo.Infrastructure.CollectionExport.Models
{
    public sealed record CollectionBackupRow
    {
        public string Name { get; init; } = string.Empty;
        public string SetCode { get; init; } = string.Empty;
        public string Uuid { get; init; } = string.Empty;
        public string? ScryfallId { get; init; }
        public string Condition { get; init; } = string.Empty;
        public string Finish { get; init; } = string.Empty;
        public string Language { get; init; } = string.Empty;
        public string? LocationName { get; init; }
        public string? LocationType { get; init; }
        public string? Comment { get; init; }
        public int CardsOwned { get; init; }
        public int CardsForTrade { get; init; }
        public decimal? SellPrice { get; init; }
        public string? SellPriceCurrency { get; init; }
    }
}
