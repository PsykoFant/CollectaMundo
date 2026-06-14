using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Filtering.Enums;
using CollectaMundo.DomainLogic.Filtering.Models;
using CollectaMundo.ViewModels.Filtering;

namespace CollectaMundo.ApplicationServices.Filtering
{
    public sealed class FacetUpdater : IFacetUpdater
    {
        private const string LocationCriteriaKey = "SelectedLocationDisplayName";

        public void RefreshFromCollection(IEnumerable<CollectionCard> collection, IReadOnlyDictionary<string, FilterItemViewModel> filters)
        {
            foreach (var (key, spec) in FilterCriteriaMappings.CriteriaMappings)
            {
                if (spec.DataSource != FilterDataSource.Collection || spec.CollectionOptionExtractor is null)
                {
                    continue;
                }

                if (!filters.TryGetValue(key, out var item) || item is null)
                {
                    continue;
                }

                if (key == LocationCriteriaKey)
                {
                    var locationOptions = collection
                        .Where(c => c.SelectedLocationId is not null &&
                                    !string.IsNullOrWhiteSpace(c.SelectedLocationDisplayName))
                        .GroupBy(c => c.SelectedLocationId!.Value)
                        .Select(g => new FilterOption(
                            g.Key.ToString(),
                            g.First().SelectedLocationDisplayName!))
                        .OrderBy(o => o.DisplayName, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    item.ResetOptions(locationOptions);
                    continue;
                }

                var values = collection.Select(spec.CollectionOptionExtractor)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                item.ResetOptions(values);
            }
        }
    }
}
