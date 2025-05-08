using CollectaMundo.DomainLogic.Models;

namespace CollectaMundo.DomainLogic
{
    public interface IEditCollectionLogic
    {
        Task AddOrUpdateCardAsync(CardSet card, bool isEdit);
        Task<CardSet> PrepareCardForListAsync(CardSet selectedCard, bool isEdit);
        Task<CardSet> PrepareNewCardWithDefaultsAsync(CardSet selectedCard);
    }
}
