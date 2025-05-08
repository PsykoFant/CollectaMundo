using CollectaMundo.DomainLogic.Models;
using System.Collections.ObjectModel;

namespace CollectaMundo.ApplicationServices
{
    public interface IEditCollectionCoordinator
    {
        Task AddCardToAddCardsListViewAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection);
        Task AddCardToEditCardsListViewAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection);
        Task<CardSet> SubmitCollectionUpdatesAsync(CardSet card, bool isEdit);
        Task<CardSet> SubmitNewCardsWithDefaultsAsync(CardSet raw, bool isEdit);
    }
}
