using CollectaMundo.Models;
using CollectaMundo.ViewModels;

namespace CollectaMundo.Services
{
    public interface ICardFilteringService
    {
        List<CardSet> ApplyFilters(IEnumerable<CardSet> cards, IEnumerable<FilterItemViewModel> filterCriteria);
    }
}
