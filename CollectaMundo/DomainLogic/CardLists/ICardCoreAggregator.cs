using CollectaMundo.DomainLogic.CardLists.Models;

namespace CollectaMundo.DomainLogic.CardLists
{
    public interface ICardCoreAggregator
    {
        List<CardCore> Aggregate(IEnumerable<CardCore> cores);
    }
}
