using CollectaMundo.DomainLogic.Shared.Models;

namespace CollectaMundo.Infrastructure.CardLists.Models
{
    public sealed class CollectionCardDbRow
    {
        public int CardId { get; init; }
        public CollectionIdentity Identity { get; init; } = default!;
        public int CardsOwned { get; init; }
        public int CardsForTrade { get; init; }
    }
}
