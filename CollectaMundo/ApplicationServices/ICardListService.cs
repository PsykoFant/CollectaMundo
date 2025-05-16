using CollectaMundo.DomainLogic.Models;

namespace CollectaMundo.ApplicationServices
{
    public interface ICardListService
    {
        Task LoadAllCardsAsync(List<CardSet> target);
        Task LoadMyCollectionAsync(List<CardSet> target);
        Task LoadAllCardsForDecksAsync(List<CardSet> target);
        Task LoadAllCardsInDecksAsync(List<CardSet> target);
        Task LoadColorIconsAsync(List<CardSet> target);

        // you’ll add LoadMyCollectionAsync, LoadCardsForDecksAsync, etc. here later
    }
}
