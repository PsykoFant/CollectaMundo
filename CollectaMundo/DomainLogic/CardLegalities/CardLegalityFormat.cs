namespace CollectaMundo.DomainLogic.CardLegalities
{
    public sealed class CardLegalityFormat
    {
        public int Id { get; init; }
        public string Value { get; init; } = string.Empty; // "modern"
        public string DisplayName { get; init; } = string.Empty; // "Modern"
        public ulong Mask { get; init; }
    }
}
