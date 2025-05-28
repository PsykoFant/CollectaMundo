using CollectaMundo.DomainLogic.CardLists.Models;

namespace CollectaMundo.DomainLogic.Filtering
{
    public interface IFilteringLogic
    {
        bool Matches(CardSet card);
    }
}
