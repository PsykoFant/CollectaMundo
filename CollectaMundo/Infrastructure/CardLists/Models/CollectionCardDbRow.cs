using CollectaMundo.DomainLogic.Shared.Models;

namespace CollectaMundo.Infrastructure.CardLists.Models
{
    public sealed class CollectionCardDbRow
    {
        public int CardId { get; init; }
        public CollectionIdentity Identity { get; init; } = default!;
        public decimal? SellPrice { get; init; }
        public string? SellPriceCurrency { get; init; }
        public int CardsOwned { get; init; }
        public int CardsForTrade { get; init; }
    }
}
