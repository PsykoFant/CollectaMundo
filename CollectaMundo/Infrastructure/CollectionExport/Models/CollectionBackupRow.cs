namespace CollectaMundo.Infrastructure.CollectionExport.Models
{
    public sealed record CollectionBackupRow
    {
        public int Id { get; init; }
        public string Uuid { get; init; } = string.Empty;
        public string Condition { get; init; } = string.Empty;
        public string Finish { get; init; } = string.Empty;
        public string Language { get; init; } = string.Empty;
        public int? LocationId { get; init; }
        public string? Comment { get; init; }
        public int CardsOwned { get; init; }
        public int CardsForTrade { get; init; }
        public decimal? SellPrice { get; init; }
        public string? SellPriceCurrency { get; init; }
    }
}
