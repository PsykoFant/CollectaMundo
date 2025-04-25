using CollectaMundo.Models;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CollectaMundo.Domain
{
    public class CardFilterService : IFilterService
    {
        public List<CardSet> ApplyFilters(
            IEnumerable<CardSet> cards,
            IEnumerable<FilterItem> criteria)
        {
            if (criteria == null || !criteria.Any())
                return cards.ToList();

            // FilterEngine is your static domain‐level filter logic
            return FilterEngine.ApplyFilter(cards, criteria);
        }
    }
}
