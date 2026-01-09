namespace CollectaMundo.DomainLogic.Import.Models
{
    public sealed class ResolvedImportItem
    {

        // Stable key used to correlate this item back to the original TempCardItem.
        public string TempItemImportKey { get; init; } = null!;

        // Explicit flag indicating whether this item can be imported.
        public bool IsImportable { get; init; }

        // Resolved UUID. May be null for unimportable items.
        public string? Uuid { get; init; }

        // Number of cards owned (non-negative integer).
        public int CardsOwned { get; init; }

        // Number of cards available for trade (non-negative integer).
        public int CardsForTrade { get; init; }

        // Resolved domain values (canonical strings)
        public string? Condition { get; init; }
        public string? Finish { get; init; }
        public string? Language { get; init; }

        // Warnings generated while resolving this item
        public IReadOnlyList<string> Warnings { get; init; } = [];
    }
}
