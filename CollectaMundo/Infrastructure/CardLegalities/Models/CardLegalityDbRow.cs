namespace CollectaMundo.Infrastructure.CardLegalities.Models
{
    public sealed class CardLegalityDbRow
    {
        public string Uuid { get; init; } = string.Empty;
        public string?[] Statuses { get; init; } = [];
    }
}
