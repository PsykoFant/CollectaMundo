using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Filtering.Enums;

namespace CollectaMundo.DomainLogic.Filtering.Models
{
    // Describes configuration for a filter criteria.
    public sealed record CriteriaSpec(
        string? ReadableLabel,
        FilterType Type,
        OperatorType[]? Operators,
        bool ShouldNotSplit,
        FilterDataSource DataSource,
        bool GenerateFilterOptions = true,
        Func<CollectionCard, string?>? CollectionOptionExtractor = null);
}
