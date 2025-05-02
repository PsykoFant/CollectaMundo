using CollectaMundo.DomainLogic.Models;

namespace CollectaMundo.DomainLogic
{
    public interface IEditCollectionLogic
    {
        Task AddOrUpdateCardAsync(CardSet card);
        Task<CardSet> PrepareCardForListAsync(CardSet selectedCard, bool isEdit);
    }


}
