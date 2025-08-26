using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Filtering.Models;

namespace CollectaMundo.Data
{
    public interface IFilterInitDefaultsRepository
    {
        List<FilterDefaults> Build(IEnumerable<CardSet> allCards, IEnumerable<CardSet> myCollection);
    }
}
