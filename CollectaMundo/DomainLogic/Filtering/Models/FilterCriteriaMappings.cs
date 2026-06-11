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
            { "Name",              new("Card Name",     FilterType.Single,  null, true,                                                     FilterDataSource.Printing) },
            { "SetName",           new("Set Name",      FilterType.Single,  null, true,                                                     FilterDataSource.Printing) },
            { "Text",              new("Rulestext",     FilterType.Single,  [OperatorType.CONTAINS, OperatorType.DOES_NOT_CONTAIN],false,   FilterDataSource.Printing) },
            { "Colors",            new("",              FilterType.Multi,   [OperatorType.OR, OperatorType.AND, OperatorType.NOT],false,    FilterDataSource.Printing) },
            { "Rarity",            new("",              FilterType.Multi,   [OperatorType.OR, OperatorType.NOT],false,                      FilterDataSource.Printing) },
            { "SuperTypes",        new("Supertypes",    FilterType.Multi,   [OperatorType.OR, OperatorType.AND, OperatorType.NOT],false,    FilterDataSource.Printing) },
            { "Types",             new("Card type",     FilterType.Multi,   [OperatorType.OR, OperatorType.AND, OperatorType.NOT],false,    FilterDataSource.Printing) },
            { "SubTypes",          new("Subtypes",      FilterType.Multi,   [OperatorType.OR, OperatorType.AND, OperatorType.NOT],false,    FilterDataSource.Printing) },
            { "Keywords",          new("",              FilterType.Multi,   [OperatorType.OR, OperatorType.AND, OperatorType.NOT],false,    FilterDataSource.Printing) },
            { "Finishes",          new("",              FilterType.Multi,   [OperatorType.OR, OperatorType.NOT],false,                      FilterDataSource.Printing) },
            { "ManaValue",         new("",              FilterType.Numeric,
                [OperatorType.GREATER_THAN,OperatorType.LESS_THAN,OperatorType.EQUALS,OperatorType.GREATER_THAN_OR_EQUALS,OperatorType.LESS_THAN_OR_EQUALS ],false,
                                                                                                                                            FilterDataSource.Printing) },    

            // === Collection-backed facets (live updates at runtime) ===
            { "SelectedFinish",                 new("Chosen finish",    FilterType.Multi,   [OperatorType.OR, OperatorType.NOT],false,                      FilterDataSource.Collection,CollectionExtractor: c => c.SelectedFinish) },
            { "Language",                       new("",                 FilterType.Multi,   [OperatorType.OR, OperatorType.NOT],false,                      FilterDataSource.Collection,CollectionExtractor: c => c.Language) },
            { "SelectedCondition",              new("Condition",        FilterType.Multi,   [OperatorType.OR, OperatorType.NOT],false,                      FilterDataSource.Collection,CollectionExtractor: c => c.SelectedCondition) },
            { "SelectedLocationDisplayName",    new("Location",         FilterType.Multi,   [OperatorType.OR, OperatorType.NOT],false,                      FilterDataSource.Collection,CollectionExtractor: c => c.SelectedLocationDisplayName) },
            { "Comment",                        new("Card comment",     FilterType.Single,  [OperatorType.CONTAINS, OperatorType.DOES_NOT_CONTAIN],false,   FilterDataSource.Collection) },
            { "CardsForTrade",                  new("",                 FilterType.Numeric, [OperatorType.GREATER_THAN, OperatorType.EQUALS],false,         FilterDataSource.Collection) }
        };
    }
}

