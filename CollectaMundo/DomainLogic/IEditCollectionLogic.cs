using CollectaMundo.DomainLogic.Models;

namespace CollectaMundo.DomainLogic
{
    public interface IEditCollectionLogic
    {
        Task<CardSet> PrepareCardForListAsync(CardSet selectedCard, bool isEdit);
        Task<CardSet> PrepareNewCardWithDefaultsAsync(CardSet selectedCard);



        Task<IReadOnlyList<CardChangeEventArgs>> SaveBatchAsync(IEnumerable<CardSet> raws, bool isEdit);
    }
}
