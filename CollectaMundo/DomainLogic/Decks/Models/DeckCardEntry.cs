namespace CollectaMundo.DomainLogic.Decks.Models
{
    public sealed class DeckCardEntry
    {
        public int DeckLocationId { get; init; }
        public string OracleId { get; init; } = string.Empty;
        public string CardName { get; init; } = string.Empty;
        public int DesiredQuantity { get; init; }
        public DeckSection Section { get; init; }
    }
}
