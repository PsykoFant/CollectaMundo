using CollectaMundo.Models;
using System.Collections.ObjectModel;

namespace CollectaMundo.UICoordinators
{
    public interface ICardCollectionCoordinator
    {
        Task AddCardToAddCardsListViewAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection);
        Task AddCardToEditCardsListViewAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection);
        Task AddOrUpdateCardAsync(CardSet card);
        Task UpdateCardDetailsAsync(CardSet card, ObservableCollection<CardSet> inMemoryCollection);
        Task DeleteCardAsync(CardSet card, ObservableCollection<CardSet> inMemoryCollection);
    }
}
