using CollectaMundo.DomainLogic.CardLists.Models; // for CardSet

namespace CollectaMundo.DomainLogic.Filtering
{
    /// <summary>
    /// Defines the type of filtering applicable to a criteria.
    /// </summary>
    public enum FilterType
    {
        Single,  // A single-selection filter (e.g., Name, SetName)
        Multi,   // A multi-selection filter (e.g., Keywords, Colors, Types)
        Numeric  // A numeric-based filter (e.g., ManaValue, CardsForTrade)
    }

    /// <summary>
    /// Describes configuration for a filter criteria.
    /// </summary>
    public sealed record CriteriaSpec(
        string? ReadableLabel,
        FilterType Type,
        OperatorType[]? Operators,
        bool ShouldNotSplit,
        bool IsCollectionFacet = false,
        Func<CardSet, string?>? SelectedExtractor = null
    );

    public static class FilterCriteriaMappings
    {
        /// <summary>
        /// Maps each filter criteria to its configuration, including UI labels,
        /// operator support, split behavior, and (optionally) how to extract
        /// values from CardSet at runtime for collection-backed facets.
        /// </summary>
        public static readonly Dictionary<string, CriteriaSpec> CriteriaMappings = new()
        {
            { "Name",              new("Card Name", FilterType.Single, null, true) },
            { "SetName",           new("Set Name",  FilterType.Single, null, true) },
            { "Text",              new("Rulestext", FilterType.Single,[OperatorType.CONTAINS, OperatorType.DOES_NOT_CONTAIN],false) },
            { "Colors",            new("", FilterType.Multi,[OperatorType.OR, OperatorType.AND, OperatorType.NOT],false) },
            { "Rarity",            new("", FilterType.Multi,[OperatorType.OR, OperatorType.NOT],false) },
            { "SuperTypes",        new("Supertypes", FilterType.Multi,[OperatorType.OR, OperatorType.AND, OperatorType.NOT],false) },
            { "Types",             new("Card type", FilterType.Multi,[OperatorType.OR, OperatorType.AND, OperatorType.NOT],false) },
            { "SubTypes",          new("Subtypes", FilterType.Multi,[OperatorType.OR, OperatorType.AND, OperatorType.NOT],false) },
            { "Keywords",          new("", FilterType.Multi,[OperatorType.OR, OperatorType.AND, OperatorType.NOT],false) },
            { "Finishes",          new("", FilterType.Multi,[OperatorType.OR, OperatorType.NOT],false) },

            // === Collection-backed facets (live updates at runtime) ===
            { "SelectedFinish",    new("Chosen finish", FilterType.Multi,[OperatorType.OR, OperatorType.NOT],false,IsCollectionFacet: true,SelectedExtractor: c => c.SelectedFinish) },

            { "Language",          new("", FilterType.Multi,[OperatorType.OR, OperatorType.NOT],false,IsCollectionFacet: true,SelectedExtractor: c => c.Language) },
            { "SelectedCondition", new("Condition", FilterType.Multi,[OperatorType.OR, OperatorType.NOT],false,IsCollectionFacet: true,SelectedExtractor: c => c.SelectedCondition) },

            // === Numeric filters ===
            { "ManaValue",         new("", FilterType.Numeric,[ OperatorType.GREATER_THAN,OperatorType.LESS_THAN,OperatorType.EQUALS,OperatorType.GREATER_THAN_OR_EQUALS,OperatorType.LESS_THAN_OR_EQUALS ],false) },
            { "CardsForTrade",     new("", FilterType.Numeric,[OperatorType.GREATER_THAN, OperatorType.EQUALS],false) }
        };
    }
}
