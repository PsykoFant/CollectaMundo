namespace CollectaMundo.Infrastructure.CardLegalities.Models
{
    namespace CollectaMundo.Infrastructure.CardLegalities.Models
    {
        public sealed class CardLegalityDbRow
        {
            public required string Uuid { get; init; }
            public required Dictionary<string, string?> Legalities { get; init; }
        }
    }
}
