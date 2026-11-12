namespace CollectaMundo.Infrastructure.CollectionExport.Models
{
    public sealed record CollectionTradeExportItem
    {
        public string Uuid { get; init; } = string.Empty;
        public string? McmId { get; init; }
        public string? ScryfallId { get; init; }
        public string? TcgplayerProductId { get; init; }

        public int CardsForTrade { get; init; }

        public string Name { get; init; } = string.Empty;
        public string SetName { get; init; } = string.Empty;
        public string Condition { get; init; } = string.Empty;
        public string Language { get; init; } = string.Empty;
        public string Finish { get; init; } = string.Empty;

        public decimal? SellPrice { get; init; }
        public string? SellPriceCurrency { get; init; }
        public string? Comment { get; init; }
    }
}
