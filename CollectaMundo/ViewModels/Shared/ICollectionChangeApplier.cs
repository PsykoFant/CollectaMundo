using CollectaMundo.DomainLogic.Shared;
using System.Collections.ObjectModel;

namespace CollectaMundo.ViewModels.Shared
{
    public interface ICollectionChangeApplier<T>
    {
        void Apply(ObservableCollection<T> collection, CollectionChangeSet<T> changes);
    }
}
