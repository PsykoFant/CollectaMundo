using CollectaMundo.DomainLogic.Shared.Models;

namespace CollectaMundo.DomainLogic.CollectionMutations.Models
{
    public sealed record UpdateCommand(int CardId, CollectionIdentity Identity, int CardsOwned, int CardsForTrade);
}
