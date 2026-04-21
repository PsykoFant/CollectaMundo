using CollectaMundo.ApplicationServices.EditCollection.Models;
using CollectaMundo.ApplicationServices.Import.Models;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.ModifyCollection.Models;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.DomainLogic.Shared.Models;
using CollectaMundo.ViewModels;

namespace CollectaMundo.DomainLogic.ModifyCollection
{
    public interface IModifyCollectionLogic
    {
        CardSet PrepareCardForList(CardSet selectedCard, CardToAddMetadataDto metadata, bool isEdit);
        CardSet PrepareNewCardWithDefaults(CardSet selectedCard, CardToAddMetadataDto metadata);
        ModifyBatchPlan PlanBatch(IEnumerable<CardSet> cards, ICollectionSnapshot snapshot, bool isEdit);
        void ApplyMyCollectionChanges(IList<CardSet> collection, CollectionChangeSet<CardSet> changes);
    }
}
