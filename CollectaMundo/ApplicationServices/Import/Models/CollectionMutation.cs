using CollectaMundo.DomainLogic.Import.Models;

namespace CollectaMundo.ApplicationServices.Import.Models
{
    public sealed class CollectionMutation
    {
        // Import never removes — but keep symmetry
        public IReadOnlyList<int> RemovedIds { get; init; } = [];

        // Identity + quantities
        public IReadOnlyList<CollectionUpsertItem> Upserts { get; init; } = [];
    }


}
