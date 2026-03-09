using CollectaMundo.ApplicationServices.EditCollection.Models;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.ModifyCollection.Models;
using CollectaMundo.DomainLogic.Shared;

namespace CollectaMundo.DomainLogic.ModifyCollection
{
    public interface IModifyCollectionLogic
    {
        CardSet PrepareCardForList(CardSet selectedCard, CardToAddMetadataDto metadata, bool isEdit);
        CardSet PrepareNewCardWithDefaults(CardSet selectedCard, CardToAddMetadataDto metadata);
        ModifyBatchPlan PlanBatch(IEnumerable<CardSet> cards, ICollectionSnapshot snapshot, bool isEdit);
    }
}
