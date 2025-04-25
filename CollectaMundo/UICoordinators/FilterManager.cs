using CollectaMundo.Models;
using CollectaMundo.ViewModels;
using System.Diagnostics;

namespace CollectaMundo.UICoordinators
{
    public static class FilterManager
    {
        public static List<CardSet> ApplyFilter(IEnumerable<CardSet> cards, IEnumerable<FilterItemViewModel> filterCriteria)
        {
            try
            {
                if (filterCriteria == null || !filterCriteria.Any())
                {
                    return [.. cards];
                }
                return [.. cards.Where(card => filterCriteria.All(filter => filter.Matches(card)))];
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while filtering cards: {ex.Message}");
                return [.. cards];
            }
        }

    }
}
