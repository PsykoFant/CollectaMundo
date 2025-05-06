using CollectaMundo.DomainLogic.Models;

namespace CollectaMundo.ApplicationServices
{
    public interface ICardListCoordinator
    {
        Task LoadAllCardsAsync(List<CardSet> target);
        Task LoadMyCollectionAsync(List<CardSet> target);
        Task LoadAllCardsForDecksAsync(List<CardSet> target);
        Task LoadAllCardsInDecksAsync(List<CardSet> target);

        // you’ll add LoadMyCollectionAsync, LoadCardsForDecksAsync, etc. here later
    }
}
