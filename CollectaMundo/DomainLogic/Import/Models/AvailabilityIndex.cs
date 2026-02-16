namespace CollectaMundo.DomainLogic.Import.Models
{
    public sealed class AvailabilityIndex
    {
        // from cards/tokens
        public IReadOnlyDictionary<string, BaseAvailability> BaseByUuid { get; init; } = new Dictionary<string, BaseAvailability>();

        // from cardForeignData (only non-English)
        public IReadOnlyDictionary<string, HashSet<string>> ForeignLanguagesByUuid { get; init; } = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
    }
    public sealed record BaseAvailability(string Uuid, string? BaseLanguage, string? FinishesCsv);

}
