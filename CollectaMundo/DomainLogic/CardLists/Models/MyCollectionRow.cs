namespace CollectaMundo.DomainLogic.CardLists.Models
{
    public sealed class MyCollectionRow
    {
        public int Id { get; init; }
        public string Uuid { get; init; } = "";
        public int CardsOwned { get; init; }
        public int CardsForTrade { get; init; }
        public string? Condition { get; init; }
        public string? Language { get; init; }
        public string? Finish { get; init; }
    }
}
