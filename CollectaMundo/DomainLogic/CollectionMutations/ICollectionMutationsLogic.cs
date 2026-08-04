using CollectaMundo.DomainLogic.CollectionMutations.Models;
using CollectaMundo.DomainLogic.Shared.CollectionSnapshot;

namespace CollectaMundo.DomainLogic.CollectionMutations
{
    public interface ICollectionMutationsLogic
    {
        CollectionMutationPlan PlanIdentityRewriteBatch(IEnumerable<CollectionCardDraft> cards, ICollectionIdentitySnapshot snapshot);
    }
}
