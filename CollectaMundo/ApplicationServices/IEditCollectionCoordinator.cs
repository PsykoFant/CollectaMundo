using CollectaMundo.DomainLogic.Models;
using System.Collections.ObjectModel;

namespace CollectaMundo.ApplicationServices
{
    public interface IEditCollectionCoordinator
    {
        Task AddCardToAddCardsListViewAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection);
        Task AddCardToEditCardsListViewAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection);
        Task<CardSet> AddOrUpdateAndFetchCardAsync(CardSet card);
        //Task UpdateCardDetailsAsync(CardSet card, ObservableCollection<CardSet> inMemoryCollection);
        //Task DeleteCardAsync(CardSet card, ObservableCollection<CardSet> inMemoryCollection);
    }
}
