using CollectaMundo.DomainLogic.Shared;

namespace CollectaMundo.ViewModels.Shared
{
    public interface ICollectionChangeApplier<T>
    {
        void Apply(IList<T> collection, CollectionChangeSet<T> changes);
    }
}
