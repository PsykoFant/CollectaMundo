using CollectaMundo.DomainLogic.Shared;

namespace CollectaMundo.ApplicationServices.Import.Models
{
    public sealed class CollectionMutation
    {
        // Import never removes today, but symmetry matters
        public IReadOnlyList<int> RemovedIds { get; init; } = [];

        // DB-truth rows: CardId + Identity + quantities
        public IReadOnlyList<MyCollectionRow> UpsertedRows { get; init; } = [];
    }
}
