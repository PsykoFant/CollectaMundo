using CollectaMundo.DomainLogic.Models;

namespace CollectaMundo.Domain
{
    public interface IFilterLogic
    {
        bool Matches(CardSet card);
    }
}
