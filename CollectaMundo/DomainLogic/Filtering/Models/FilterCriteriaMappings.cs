using CollectaMundo.DomainLogic.Filtering.Enums;

namespace CollectaMundo.DomainLogic.Filtering.Models
{
    public static class FilterCriteriaMappings
    {
        /// <summary> 
        /// Maps each filter criteria to its configuration, including UI labels,
        /// operator support, split behavior, and (optionally) how to extract
        /// values from CardSet at runtime for collection-backed facets.
        /// </summary>
        public static readonly Dictionary<string, CriteriaSpec> CriteriaMappings = new()
        {
            { "Name",              new("Card Name",         FilterType.Single,  null, true,                                                     FilterDataSource.Printing, GenerateFilterOptions: true) },
            { "SetName",           new("Set Name",          FilterType.Single,  null, true,                                                     FilterDataSource.Printing, GenerateFilterOptions: true) },
            { "Text",              new("Rulestext",         FilterType.Single,  [OperatorType.CONTAINS, OperatorType.DOES_NOT_CONTAIN],false,   FilterDataSource.Printing, GenerateFilterOptions: false) },
            { "Colors",            new("",                  FilterType.Multi,   [OperatorType.OR, OperatorType.AND, OperatorType.NOT],false,    FilterDataSource.Printing, GenerateFilterOptions: false) },
            { "Rarity",            new("",                  FilterType.Multi,   [OperatorType.OR, OperatorType.NOT],false,                      FilterDataSource.Printing, GenerateFilterOptions: true) },
            { "SuperTypes",        new("Supertypes",        FilterType.Multi,   [OperatorType.OR, OperatorType.AND, OperatorType.NOT],false,    FilterDataSource.Printing, GenerateFilterOptions: true) },
            { "Types",             new("Card type",         FilterType.Multi,   [OperatorType.OR, OperatorType.AND, OperatorType.NOT],false,    FilterDataSource.Printing, GenerateFilterOptions: true) },
            { "SubTypes",          new("Subtypes",          FilterType.Multi,   [OperatorType.OR, OperatorType.AND, OperatorType.NOT],false,    FilterDataSource.Printing, GenerateFilterOptions: true) },
            { "Keywords",          new("",                  FilterType.Multi,   [OperatorType.OR, OperatorType.AND, OperatorType.NOT],false,    FilterDataSource.Printing, GenerateFilterOptions: true) },
            { "Finishes",          new("",                  FilterType.Multi,   [OperatorType.OR, OperatorType.NOT],false,                      FilterDataSource.Printing, GenerateFilterOptions: false) },
            { "Availability",      new("",                  FilterType.Multi,   [OperatorType.OR, OperatorType.AND, OperatorType.NOT],false,    FilterDataSource.Printing, GenerateFilterOptions: true) },
            { "ManaValue",         new("",                  FilterType.Numeric,
                [OperatorType.GREATER_THAN,OperatorType.LESS_THAN,OperatorType.EQUALS,OperatorType.GREATER_THAN_OR_EQUALS,OperatorType.LESS_THAN_OR_EQUALS ],false,
                                                                                                                                            FilterDataSource.Printing, GenerateFilterOptions: true) },    
            // === Collection-backed facets (live updates at runtime) ===
            { "SelectedFinish",                 new("Chosen finish",    FilterType.Multi,   [OperatorType.OR, OperatorType.NOT],false,                      FilterDataSource.Collection, GenerateFilterOptions: true, CollectionOptionExtractor : c => c.SelectedFinish) },
            { "Language",                       new("",                 FilterType.Multi,   [OperatorType.OR, OperatorType.NOT],false,                      FilterDataSource.Collection, GenerateFilterOptions: true, CollectionOptionExtractor : c => c.Language) },
            { "SelectedCondition",              new("Condition",        FilterType.Multi,   [OperatorType.OR, OperatorType.NOT],false,                      FilterDataSource.Collection, GenerateFilterOptions: true, CollectionOptionExtractor : c => c.SelectedCondition) },
            { "SelectedLocationDisplayName",    new("Location",         FilterType.Multi,   [OperatorType.OR, OperatorType.NOT],false,                      FilterDataSource.Collection, GenerateFilterOptions: true, CollectionOptionExtractor : c => c.SelectedLocationDisplayName) },
            { "Comment",                        new("Card comment",     FilterType.Single,  [OperatorType.CONTAINS, OperatorType.DOES_NOT_CONTAIN],false,   FilterDataSource.Collection, GenerateFilterOptions: false) },
            { "CardsForTrade",                  new("",                 FilterType.Numeric, [OperatorType.GREATER_THAN, OperatorType.EQUALS],false,         FilterDataSource.Collection, GenerateFilterOptions: false) }
        };
    }
}

