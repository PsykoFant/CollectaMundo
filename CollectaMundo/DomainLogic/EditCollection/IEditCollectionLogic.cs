using CollectaMundo.ApplicationServices.EditCollection.Models;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.EditCollection.Models;
using CollectaMundo.DomainLogic.Shared;
using System.Data.SQLite;

namespace CollectaMundo.DomainLogic.EditCollection
{
    public interface IEditCollectionLogic
    {
        CardSet PrepareCardForList(CardSet selectedCard, CardToAddMetadataDto metadata, bool isEdit);
        CardSet PrepareNewCardWithDefaults(CardSet selectedCard, CardToAddMetadataDto metadata);
        EditBatchPlan PlanBatch(IEnumerable<CardSet> cards, ICollectionSnapshot snapshot, bool isEdit);
    }
}
