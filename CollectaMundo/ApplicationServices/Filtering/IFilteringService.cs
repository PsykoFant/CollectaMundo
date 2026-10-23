using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.ViewModels.Filtering;

namespace CollectaMundo.ApplicationServices.Filtering
{
    public interface IFilteringService
    {
        List<PrintingCard> ApplyFilters(IEnumerable<PrintingCard> cards, IEnumerable<FilterItemViewModel> vmFilters);
        List<CollectionCard> ApplyFilters(IEnumerable<CollectionCard> cards, IEnumerable<FilterItemViewModel> vmFilters);
        List<OracleCard> ApplyFilters(IEnumerable<OracleCard> cards, IEnumerable<FilterItemViewModel> vmFilters);
        void ResetAllFilters(IEnumerable<FilterItemViewModel> filters);
        string BuildSummary(IEnumerable<FilterItemViewModel> filters);
    }
}
