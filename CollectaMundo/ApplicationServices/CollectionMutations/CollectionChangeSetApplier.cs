using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.CollectionMutations;
using CollectaMundo.DomainLogic.Shared.Models;

namespace CollectaMundo.ApplicationServices.CollectionMutations
{
    public sealed class CollectionChangeSetApplier(ICollectionMutationsLogic planner) : ICollectionChangeSetApplier
    {
        private readonly ICollectionMutationsLogic _logic = planner;
        public void Apply(IList<CardSet> collection, CollectionChangeSet<CardSet> changes)
        {
            _logic.ApplyCollectionChangeSet(collection, changes);
        }
    }
}
