namespace CollectaMundo.DomainLogic.CardPrices
{
    public class CardPrice
    {
        public string Uuid { get; init; } = string.Empty;
        public string Format { get; init; } = string.Empty;   // e.g., "paper", "mtgo"
        public string Retailer { get; init; } = string.Empty; // e.g., "tcgplayer"
        public string Finish { get; init; } = string.Empty;   // "normal", "foil", "etched"
        public decimal Price { get; init; }
    }
}
