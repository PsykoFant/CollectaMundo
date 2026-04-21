using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Shared.Models;

namespace CollectaMundo.ApplicationServices.CollectionMutations
{
    public interface ICollectionChangeSetApplier
    {
        void Apply(IList<CardSet> collection, CollectionChangeSet<CardSet> changes);
    }
}
