using CollectaMundo.Models;
using CollectaMundo.ViewModels;

namespace CollectaMundo.UICoordinators
{
    public interface IFilteringCoordinator
    {
        List<CardSet> ApplyFilters(IEnumerable<CardSet> cards, IEnumerable<FilterItemViewModel> criteria);
        void ResetAllFilters(IEnumerable<FilterItemViewModel> filters);
        string BuildSummary(IEnumerable<FilterItemViewModel> filters);
    }
}
