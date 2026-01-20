using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Shared;
using System.Collections.ObjectModel;

namespace CollectaMundo.ApplicationServices.EditCollection
{
    public interface IEditCollectionService
    {
        Task AddCardToAddCardsListViewAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection);
        Task AddCardToEditCardsListViewAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection);
        Task<CollectionChangeSet<CardSet>> SubmitNewCardsWithDefaultsBatchAsync(IEnumerable<CardSet> cards);
        Task<CollectionChangeSet<CardSet>> SubmitCardBatchAsync(IEnumerable<CardSet> cards, ICollectionSnapshot snapshot);
    }
}
