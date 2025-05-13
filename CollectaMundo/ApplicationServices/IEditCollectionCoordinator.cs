using CollectaMundo.DomainLogic.Models;
using System.Collections.ObjectModel;

namespace CollectaMundo.ApplicationServices
{
    public interface IEditCollectionCoordinator
    {
        Task AddCardToAddCardsListViewAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection);
        Task AddCardToEditCardsListViewAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection);
        Task<CardChangeEventArgs> SubmitCollectionUpdatesAsync(CardSet card, bool isEdit);
        Task<CardChangeEventArgs> SubmitNewCardsWithDefaultsAsync(CardSet raw);
        Task<IReadOnlyList<CardChangeEventArgs>> SubmitCollectionBatchAsync(
            IEnumerable<(CardSet card, bool isEdit)> items);
    }
}
