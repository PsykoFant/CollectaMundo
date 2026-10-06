using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Filtering.Models;
using CollectaMundo.DomainLogic.Shared.CardModels;

namespace CollectaMundo.DomainLogic.Filtering
{
    public interface IFilterDefaultsLogic
    {
        List<FilterDefaults> BuildFilters(IReadOnlyList<PrintingCard> allCards, IReadOnlyList<CollectionCard> myCollection);
    }
}
