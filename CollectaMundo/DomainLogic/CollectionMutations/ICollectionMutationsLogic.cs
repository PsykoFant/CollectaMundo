using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.CollectionMutations.Models;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.DomainLogic.Shared.Models;

namespace CollectaMundo.DomainLogic.CollectionMutations
{
    public interface ICollectionMutationsLogic
    {
        CollectionMutationPlan PlanIdentityRewriteBatch(IEnumerable<CardSet> cards, ICollectionSnapshot snapshot, bool isEdit);
        void ApplyCollectionChangeSet(IList<CardSet> collection, CollectionChangeSet<CardSet> changes);
    }
}
