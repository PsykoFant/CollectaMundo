namespace CollectaMundo.Infrastructure.CardLegalities.Models
{
    public sealed class CardLegalityReadResult
    {
        public string[] FormatNames { get; init; } = [];
        public IReadOnlyList<CardLegalityDbRow> Rows { get; init; } = [];
    }
}
