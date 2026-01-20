namespace CollectaMundo.DomainLogic.Shared
{
    public sealed class MyCollectionRow
    {
        public int CardId { get; init; }
        public CollectionIdentity Identity { get; init; } = default!;
        public int CardsOwned { get; init; }
        public int CardsForTrade { get; init; }
    }
}
