using CollectaMundo.Models;
using CollectaMundo.ViewModels;

namespace CollectaMundo.UICoordinators
{
    public interface IFilteringCoordinator
    {
        List<CardSet> ApplyFilters(IEnumerable<CardSet> cards, IEnumerable<FilterItemViewModel> vmFilters);
        void ResetAllFilters(IEnumerable<FilterItemViewModel> filters);
        string BuildSummary(IEnumerable<FilterItemViewModel> filters);
        Task<List<FilterDefaults>> LoadDefaultsAsync();
    }
}
