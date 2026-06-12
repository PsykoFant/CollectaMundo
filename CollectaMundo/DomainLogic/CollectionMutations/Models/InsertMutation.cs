using CollectaMundo.DomainLogic.Shared.Models;

namespace CollectaMundo.DomainLogic.CollectionMutations.Models
{
    public sealed record InsertMutation(CollectionIdentity Identity, int CardsOwned, int CardsForTrade, CollectionCardDraft Draft)
    {
        public int? AssignedCardId { get; private set; }
        public void BindCardId(int id)
        {
            AssignedCardId = id;
            Draft.CardId = id;
        }
    }
}
