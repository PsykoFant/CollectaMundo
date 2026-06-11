using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Filtering.Models;

namespace CollectaMundo.DomainLogic.Filtering
{
    public interface IFilterDefaultsLogic
    {
        List<FilterDefaults> Build(IReadOnlyList<PrintingCard> allCards, IReadOnlyList<CollectionCard> myCollection);
    }
}
