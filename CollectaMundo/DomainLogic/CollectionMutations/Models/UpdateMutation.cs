using CollectaMundo.DomainLogic.Shared.Models;

namespace CollectaMundo.DomainLogic.CollectionMutations.Models
{
    public sealed record UpdateMutation(int CardId, CollectionIdentity Identity, int CardsOwned, int CardsForTrade);
}
