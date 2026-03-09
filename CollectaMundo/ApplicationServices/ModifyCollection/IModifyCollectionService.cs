using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Shared;

namespace CollectaMundo.ApplicationServices.ModifyCollection
{
    public interface IModifyCollectionService
    {
        Task<CardSet> CreateCardForAddAsync(CardSet selectedCard);
        Task<CardSet> CreateCardForEditAsync(CardSet selectedCard);
        Task<CollectionChangeSet<CardSet>> SubmitCardBatchAsync(IEnumerable<CardSet> cards, ICollectionSnapshot snapshot);
        Task<CollectionChangeSet<CardSet>> SubmitNewCardsWithDefaultsBatchAsync(IEnumerable<CardSet> cards, ICollectionSnapshot snapshot);
    }
}
