using CollectaMundo.Models;
using CollectaMundo.ViewModels;

namespace CollectaMundo.UICoordinators
{
    public interface IFilterService
    {
        List<CardSet> ApplyFilters(IEnumerable<CardSet> cards, IEnumerable<FilterItemViewModel> criteria);
    }
}
