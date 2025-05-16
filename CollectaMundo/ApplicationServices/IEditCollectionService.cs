using CollectaMundo.DomainLogic.Models;
using System.Collections.ObjectModel;

namespace CollectaMundo.ApplicationServices
{
    public interface IEditCollectionService
    {
        Task AddCardToAddCardsListViewAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection);
        Task AddCardToEditCardsListViewAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection);
        Task<List<CardChangeEventArgs>> SubmitNewCardsWithDefaultsBatchAsync(IEnumerable<CardSet> cards);
        Task<List<CardChangeEventArgs>> SubmitCardBatchAsync(IEnumerable<CardSet> cards);
    }
}
