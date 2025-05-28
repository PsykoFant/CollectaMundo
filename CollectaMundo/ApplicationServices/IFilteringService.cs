using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.ViewModels;

namespace CollectaMundo.ApplicationServices
{
    public interface IFilteringService
    {
        List<CardSet> ApplyFilters(IEnumerable<CardSet> cards, IEnumerable<FilterItemViewModel> vmFilters);
        void ResetAllFilters(IEnumerable<FilterItemViewModel> filters);
        string BuildSummary(IEnumerable<FilterItemViewModel> filters);
    }
}
