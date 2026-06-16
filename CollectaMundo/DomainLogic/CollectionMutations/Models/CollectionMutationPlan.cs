using CollectaMundo.DomainLogic.Shared.Models;
using CollectaMundo.Infrastructure.Shared.Models;

namespace CollectaMundo.DomainLogic.CollectionMutations.Models
{
    public sealed class CollectionMutationPlan
    {
        public List<int> DeleteIds { get; } = [];
        public List<UpdateMutation> Updates { get; } = [];
        public List<InsertMutation> Inserts { get; } = [];
        public Dictionary<CollectionIdentity, CollectionCardDbRow> UpsertsByIdentity { get; } = [];
        public CollectionChangeSet<CollectionCardDbRow> ChangeSet { get; set; } = new();
    }
}
