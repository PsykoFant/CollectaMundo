using CollectaMundo.DomainLogic.Models;

namespace CollectaMundo.Data
{
    public interface ICardListRepository
    {
        Task<IReadOnlyList<CardSet>> GetAllCardsAsync();
        Task<IReadOnlyList<CardSet>> GetMyCollectionAsync();
        Task<IReadOnlyList<CardSet>> GetCardsForDecksAsync();
        Task<IReadOnlyList<CardSet>> GetCardsInDecksAsync();
        Task<IReadOnlyList<CardSet>> GetColorIconsAsync();
    }

}
