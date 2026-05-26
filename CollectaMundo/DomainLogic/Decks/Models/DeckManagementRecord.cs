namespace CollectaMundo.DomainLogic.Decks.Models
{
    public sealed class DeckManagementRecord
    {
        public int LocationId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Format { get; init; }
        public string? Description { get; init; }
    }
}
