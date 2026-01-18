using CollectaMundo.DomainLogic.Import.Models;
using CollectaMundo.DomainLogic.Shared;

namespace CollectaMundo.ViewModels.Shared
{
    public interface ICollectionChangeApplier<T>
    {
        void Apply(IList<T> collection, CollectionChangeSet<T> changes);

        void ApplyImportUpserts(IList<T> collection, IReadOnlyList<CollectionUpsertItem> upserts);
    }
}
