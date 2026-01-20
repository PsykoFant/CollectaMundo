using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Shared;

namespace CollectaMundo.DomainLogic.EditCollection.Models
{
    public sealed class EditBatchPlan
    {
        public List<int> DeleteIds { get; } = [];
        public List<UpdateCommand> Updates { get; } = [];
        public List<InsertCommand> Inserts { get; } = [];
        public CollectionChangeSet<CardSet> ChangeSet { get; set; } = new();
    }
    public sealed record UpdateCommand(int CardId, CollectionIdentity Identity, int CardsOwned, int CardsForTrade);
    public sealed record InsertCommand(CollectionIdentity Identity, int CardsOwned, int CardsForTrade)
    {
        // Filled in later by service
        public int? AssignedCardId { get; private set; }
        public void BindCardId(int id) => AssignedCardId = id;
    }
}
