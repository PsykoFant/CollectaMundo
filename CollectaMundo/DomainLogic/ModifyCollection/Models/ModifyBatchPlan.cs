using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Shared.Models;

namespace CollectaMundo.DomainLogic.ModifyCollection.Models
{
    public sealed class ModifyBatchPlan
    {
        public List<int> DeleteIds { get; } = [];
        public List<UpdateCommand> Updates { get; } = [];
        public List<InsertCommand> Inserts { get; } = [];
        public CollectionChangeSet<CardSet> ChangeSet { get; set; } = new();
    }
    public sealed record UpdateCommand(int CardId, CollectionIdentity Identity, int CardsOwned, int CardsForTrade);
    public sealed record InsertCommand(CollectionIdentity Identity, int CardsOwned, int CardsForTrade, CardSet Card)
    {
        public int? AssignedCardId { get; private set; }
        public void BindCardId(int id)
        {
            AssignedCardId = id;
            Card.CardId = id;
        }
    }

}
