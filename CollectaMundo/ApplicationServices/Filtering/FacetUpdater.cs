using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.ViewModels;

namespace CollectaMundo.ApplicationServices.Filtering
{
    public sealed class FacetUpdater : IFacetUpdater
    {
        // centralize keys
        private const string KeyCondition = "SelectedCondition";
        private const string KeyLanguage = "Language";
        private const string KeyFinish = "SelectedFinish";

        public void RefreshFromCollection(IEnumerable<CardSet> collection, IReadOnlyDictionary<string, FilterItemViewModel> filters)
        {
            var conditions = DistinctSorted(collection.Select(c => c.SelectedCondition));
            var languages = DistinctSorted(collection.Select(c => c.Language));
            var finishes = DistinctSorted(collection.Select(c => c.SelectedFinish));

            Apply(KeyCondition, conditions, filters);
            Apply(KeyLanguage, languages, filters);
            Apply(KeyFinish, finishes, filters);
        }

        private static List<string> DistinctSorted(IEnumerable<string?> src) =>
            [.. src.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s, StringComparer.OrdinalIgnoreCase)];

        private static void Apply(string key, IReadOnlyList<string> values, IReadOnlyDictionary<string, FilterItemViewModel> filters)
        {
            if (!filters.TryGetValue(key, out var item) || item is null)
            {
                return;
            }

            item.ResetOptions(values); // lets the VM rewire handlers + refresh FilteredOptions
        }
    }
}
