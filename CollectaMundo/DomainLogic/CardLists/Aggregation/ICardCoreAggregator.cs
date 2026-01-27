using CollectaMundo.DomainLogic.CardLists.Models;

namespace CollectaMundo.DomainLogic.CardLists.Aggregation
{
    public interface ICardCoreAggregator
    {
        List<CardCore> Aggregate(IEnumerable<CardCore> cores);
    }
}
