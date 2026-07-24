namespace CollectaMundo.DomainLogic.Decks.Models
{
    public sealed class DeckBuildingRuleContext
    {
        public string? Format { get; init; }
        public IReadOnlyList<DeckBuildingRuleEntry> Entries { get; init; } = [];
    }
}
