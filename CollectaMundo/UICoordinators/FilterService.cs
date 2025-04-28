using CollectaMundo.Models;
using CollectaMundo.ViewModels;
using System.Diagnostics;

namespace CollectaMundo.UICoordinators
{
    public class FilterService : IFilterService
    {
        public List<CardSet> ApplyFilters(
            IEnumerable<CardSet> cards,
            IEnumerable<FilterItemViewModel> criteria)
        {
            if (criteria == null || !criteria.Any())
                return cards.ToList();

            return RunFilter(cards, criteria);
        }
        private static List<CardSet> RunFilter(
            IEnumerable<CardSet> cards,
            IEnumerable<FilterItemViewModel> criteria)
        {
            try
            {
                return cards
                    .Where(card => criteria.All(f => f.Matches(card)))
                    .ToList();
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"Filter error: {ex.Message}");
                return cards.ToList();
            }
        }
    }
}
