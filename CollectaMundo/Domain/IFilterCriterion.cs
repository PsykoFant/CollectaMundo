using CollectaMundo.Models;

namespace CollectaMundo.Domain
{
    public interface IFilterCriterion
    {
        bool Matches(CardSet card);
    }
}
