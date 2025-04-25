using CollectaMundo.Models;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CollectaMundo.Domain
{
    public interface IFilterService
    {
        List<CardSet> ApplyFilters(IEnumerable<CardSet> cards, IEnumerable<FilterItem> criteria);
    }
}
