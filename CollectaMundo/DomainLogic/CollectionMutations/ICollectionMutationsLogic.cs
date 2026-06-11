using CollectaMundo.DomainLogic.CollectionMutations.Models;
using CollectaMundo.DomainLogic.Shared;

namespace CollectaMundo.DomainLogic.CollectionMutations
{
    public interface ICollectionMutationsLogic
    {
        CollectionMutationPlan PlanIdentityRewriteBatch(IEnumerable<CollectionCardDraft> cards, ICollectionSnapshot snapshot);
    }
}
