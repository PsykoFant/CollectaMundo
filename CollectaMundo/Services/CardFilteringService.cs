using CollectaMundo.UICoordinators;
using CollectaMundo.Models;
using CollectaMundo.ViewModels;
using System.Diagnostics;

namespace CollectaMundo.Services
{
    public class CardFilteringService : ICardFilteringService
    {
        public List<CardSet> ApplyFilters(IEnumerable<CardSet> cards, IEnumerable<FilterItemViewModel> filterCriteria)
        {
            try
            {
                // Reuse the existing FilterManager logic.
                return FilterManager.ApplyFilter(cards, filterCriteria);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in filtering service: {ex.Message}");
                return [.. cards];
            }
        }
    }
}
