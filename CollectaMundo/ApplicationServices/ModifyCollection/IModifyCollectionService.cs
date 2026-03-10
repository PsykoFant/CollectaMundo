using CollectaMundo.ApplicationServices.Import.Models;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.ViewModels;

namespace CollectaMundo.ApplicationServices.ModifyCollection
{
    public interface IModifyCollectionService
    {
        Task<CardSet> CreateCardForAddAsync(CardSet selectedCard);
        Task<CardSet> CreateCardForEditAsync(CardSet selectedCard);
        Task<CollectionChangeSet<CardSet>> SubmitCardBatchAsync(IEnumerable<CardSet> cards, ICollectionSnapshot snapshot);
        Task<CollectionChangeSet<CardSet>> SubmitNewCardsWithDefaultsBatchAsync(IEnumerable<CardSet> cards, ICollectionSnapshot snapshot);
        CollectionChangeSet<CardSet> BuildCollectionChangeSet(CollectionMutation mutation, CardViewModel myCollection, CardViewModel allCards);
        void ApplyMyCollectionChanges(IList<CardSet> collection, CollectionChangeSet<CardSet> changes);
    }
}
