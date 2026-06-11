using CollectaMundo.DomainLogic.Shared.Models;

namespace CollectaMundo.DomainLogic.CollectionMutations.Models
{
    public sealed class CollectionMutationPlan
    {
        public List<int> DeleteIds { get; } = [];
        public List<UpdateCommand> Updates { get; } = [];
        public List<InsertCommand> Inserts { get; } = [];
        public CollectionChangeSet<MyCollectionRow> ChangeSet { get; set; } = new();
    }
}
