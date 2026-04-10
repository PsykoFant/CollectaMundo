using CollectaMundo.DomainLogic.CardLists.Models;

namespace CollectaMundo.DomainLogic.Shared
{
    public sealed class CollectionSnapshot : ICollectionSnapshot
    {
        private readonly Dictionary<int, MyCollectionRow> _byId;
        private readonly Dictionary<CollectionIdentity, MyCollectionRow> _byIdentity;

        private CollectionSnapshot(Dictionary<int, MyCollectionRow> byId,Dictionary<CollectionIdentity, MyCollectionRow> byIdentity)
        {
            _byId = byId;
            _byIdentity = byIdentity;
        }

        public bool TryGetById(int cardId, out MyCollectionRow row) => _byId.TryGetValue(cardId, out row!);
        public bool TryGetByIdentity(CollectionIdentity identity, out MyCollectionRow row) => _byIdentity.TryGetValue(identity, out row!);
        public static CollectionSnapshot From(IEnumerable<CardSet> cards)
        {
            var byId = new Dictionary<int, MyCollectionRow>(capacity: 1024);
            var byIdentity = new Dictionary<CollectionIdentity, MyCollectionRow>(capacity: 1024);

            foreach (var card in cards)
            {
                if (card.CardId is not int cardId)
                {
                    continue;
                }

                var identity = CollectionIdentityFactory.Create(card.Uuid,card.SelectedCondition,card.Language,card.SelectedFinish,card.SelectedLocationId,card.Comment);

                var row = new MyCollectionRow
                {
                    CardId = cardId,
                    Identity = identity,
                    CardsOwned = card.CardsOwned,
                    CardsForTrade = card.CardsForTrade
                };

                byId[cardId] = row;
                byIdentity[identity] = row;
            }

            return new CollectionSnapshot(byId, byIdentity);
        }
    }
}
