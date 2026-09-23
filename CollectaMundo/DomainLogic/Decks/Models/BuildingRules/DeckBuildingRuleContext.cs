namespace CollectaMundo.DomainLogic.Decks.Models.BuildingRules
{
    public sealed class DeckBuildingRuleContext
    {
        public string? Format { get; init; }
        public IReadOnlyList<DeckBuildingRuleEntry> Entries { get; init; } = [];
    }
}
