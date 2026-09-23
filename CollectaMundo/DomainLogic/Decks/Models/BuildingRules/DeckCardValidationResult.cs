namespace CollectaMundo.DomainLogic.Decks.Models.BuildingRules
{
    public sealed class DeckCardValidationResult
    {
        public bool IsLegal { get; init; } = true;
        public string? Message { get; init; }
    }
}
