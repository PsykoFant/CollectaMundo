namespace CollectaMundo.DomainLogic.CardLists.Models
{
    public sealed class CardCore
    {
        public required string Uuid { get; init; }
        public required string Name { get; init; }
        public string? SetName { get; init; }
        public string? SetCode { get; init; }
        public DateTime? ReleaseDate { get; init; }
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


        // Prices
        public decimal? NormalPrice { get; init; }
        public decimal? FoilPrice { get; init; }
        public decimal? EtchedPrice { get; init; }
    }

}
