namespace CollectaMundo.DomainLogic.Shared
{
    // Describes how the in-memory collection should be updated after a mutation (edit, import, etc.).
    public sealed class CollectionChangeSet<T>
    {
        // CardIds to remove from in-memory collections
        public IReadOnlyList<int> RemovedIds { get; init; } = [];

        // Cards to add or update in-memory
        public IReadOnlyList<T> AddedOrUpdated { get; init; } = [];
    }

}
