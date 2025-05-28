using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.EditCollection.Models;

namespace CollectaMundo.DomainLogic.EditCollection
{
    public interface IEditCollectionLogic
    {
        Task<CardSet> PrepareCardForListAsync(CardSet selectedCard, bool isEdit);
        Task<CardSet> PrepareNewCardWithDefaultsAsync(CardSet selectedCard);
        Task<IReadOnlyList<CardChangeEventArgs>> SaveBatchAsync(IEnumerable<CardSet> raws, bool isEdit);
    }
}
