using CollectaMundo.DomainLogic.Models;

namespace CollectaMundo.Data
{
    public interface ICardListRepository
    {
        Task<IReadOnlyList<CardSet>> GetAllCardsAsync();
        Task<IReadOnlyList<CardSet>> GetMyCollectionAsync();
    }

}
