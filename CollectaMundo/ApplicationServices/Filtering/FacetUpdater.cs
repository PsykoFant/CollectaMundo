using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Filtering;
using CollectaMundo.ViewModels;

namespace CollectaMundo.ApplicationServices.Filtering
{
    public sealed class FacetUpdater : IFacetUpdater
    {
        public void RefreshFromCollection(IEnumerable<CardSet> collection, IReadOnlyDictionary<string, FilterItemViewModel> filters)
        {
            foreach (var (key, spec) in FilterCriteriaMappings.CriteriaMappings)
            {
                if (!spec.IsCollectionFacet || spec.SelectedExtractor is null)
                {
                    continue;
                }

                var values = collection
                    .Select(spec.SelectedExtractor)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (filters.TryGetValue(key, out var item) && item is not null)
                {
                    item.ResetOptions(values);
                }
            }
        }
    }
}
