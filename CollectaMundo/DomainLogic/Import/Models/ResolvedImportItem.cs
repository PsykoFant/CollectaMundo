namespace CollectaMundo.DomainLogic.Import.Models
{
    public sealed class ResolvedImportItem
    {

        // Stable key used to correlate this item back to the original TempCardItem.
        public string TempItemImportKey { get; init; } = null!;

        // Explicit flag indicating whether this item can be imported.
        public bool IsImportable { get; set; }

        // Resolved UUID. May be null for unimportable items.
        public string? Uuid { get; init; }

        // Number of cards owned (non-negative integer).
        public int CardsOwned { get; init; }

        // Number of cards available for trade (non-negative integer).
        public int CardsForTrade { get; init; }

        // Resolved domain values (canonical strings)
        public string? Condition { get; init; }

        private string? _finish;
        public string? Finish
        {
            get => _finish;
            init => _finish = value;
        }

        private string? _language;
        public string? Language
        {
            get => _language;
            init => _language = value;
        }

        // Warnings generated while resolving this item
        private readonly List<string> _warnings = [];
        public IReadOnlyList<string> Warnings => _warnings;
        public void AddWarning(string warning)
        {
            _warnings.Add(warning);
        }
        public void AddWarnings(IEnumerable<string> warnings)
        {
            foreach (var w in warnings)
            {
                AddWarning(w);
            }
        }
        public void OverwriteFinish(string finish)
        {
            _finish = finish;
        }
        public void OverwriteLanguage(string language)
        {
            _language = language;
        }
    }
}
