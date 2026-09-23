namespace CollectaMundo.DomainLogic.Decks.Models.Stats
{
    public sealed record DeckStatsBucket
    {
        public required string Label { get; init; }
        public int Count { get; init; }
    }
}
