namespace CollectaMundo.Infrastructure.CardLocations.Models
{
    public sealed class CardLocationDbRow
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Type { get; init; } = string.Empty;
    }
}
