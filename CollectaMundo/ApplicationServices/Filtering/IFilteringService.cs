using CollectaMundo.ViewModels.Filtering;

namespace CollectaMundo.ApplicationServices.Filtering
{
    public interface IFilteringService
    {
        List<TCard> ApplyFilters<TCard>(IEnumerable<TCard> cards, IEnumerable<FilterItemViewModel> vmFilters, bool gameplayCardsOnly);
        void ResetAllFilters(IEnumerable<FilterItemViewModel> filters);
        string BuildSummary(IEnumerable<FilterItemViewModel> filters, bool gameplayCardsOnly);
    }
}
