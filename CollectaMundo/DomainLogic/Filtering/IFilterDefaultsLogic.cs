using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Filtering.Models;

namespace CollectaMundo.DomainLogic.Filtering
{
    public interface IFilterDefaultsLogic
    {
        List<FilterDefaults> Build(IEnumerable<CardSet> allCards, IEnumerable<CardSet> myCollection);
    }
}
