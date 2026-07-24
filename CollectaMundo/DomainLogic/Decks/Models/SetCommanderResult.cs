namespace CollectaMundo.DomainLogic.Decks.Models
{
    public sealed class SetCommanderResult
    {
        public bool Succeeded { get; init; }
        public string? Message { get; init; }
        public IReadOnlyList<DeckCardState> Cards { get; init; } = [];
    }
}
