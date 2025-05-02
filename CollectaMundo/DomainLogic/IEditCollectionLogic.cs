using CollectaMundo.DomainLogic.Models;

namespace CollectaMundo.Domain
{
    namespace CollectaMundo.Domain
    {
        public interface IEditCollectionLogic
        {
            Task AddOrUpdateCardAsync(CardSet card);
            Task<CardSet> PrepareCardForListAsync(CardSet selectedCard, bool isEdit);
        }
    }

}
