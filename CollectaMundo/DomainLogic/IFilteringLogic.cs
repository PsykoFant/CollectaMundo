using CollectaMundo.DomainLogic.Models;

namespace CollectaMundo.Domain
{
    public interface IFilteringLogic
    {
        bool Matches(CardSet card);
    }
}
