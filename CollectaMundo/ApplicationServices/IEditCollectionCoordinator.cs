using CollectaMundo.DomainLogic.Models;
using System.Collections.ObjectModel;

namespace CollectaMundo.UICoordinators
{
    public interface IEditCollectionCoordinator
    {
        Task AddCardToAddCardsListViewAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection);
        Task AddCardToEditCardsListViewAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection);
        //Task UpdateCardDetailsAsync(CardSet card, ObservableCollection<CardSet> inMemoryCollection);
        //Task DeleteCardAsync(CardSet card, ObservableCollection<CardSet> inMemoryCollection);
    }
}
