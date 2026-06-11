using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Shared.Models;

namespace CollectaMundo.DomainLogic.Shared
{
    public sealed class CollectionSnapshot : ICollectionSnapshot
    {
        private readonly Dictionary<int, MyCollectionRow> _byId;
        private readonly Dictionary<CollectionIdentity, MyCollectionRow> _byIdentity;
        private CollectionSnapshot(Dictionary<int, MyCollectionRow> byId, Dictionary<CollectionIdentity, MyCollectionRow> byIdentity)
        {
            _byId = byId;
            _byIdentity = byIdentity;
        }
        public IReadOnlyCollection<MyCollectionRow> Rows => _byId.Values;
        public bool TryGetById(int cardId, out MyCollectionRow row) => _byId.TryGetValue(cardId, out row!);
        public bool TryGetByIdentity(CollectionIdentity identity, out MyCollectionRow row) => _byIdentity.TryGetValue(identity, out row!);
        public static CollectionSnapshot From(IEnumerable<CollectionCard> cards)
        {
            var byId = new Dictionary<int, MyCollectionRow>(capacity: 1024);
            var byIdentity = new Dictionary<CollectionIdentity, MyCollectionRow>(capacity: 1024);

            foreach (var card in cards)
            {
                var identity = CollectionIdentityFactory.Create(
                    card.Uuid,
                    card.SelectedCondition,
                    card.Language,
                    card.SelectedFinish,
                    card.SelectedLocationId,
                    card.Comment);

                var row = new MyCollectionRow
                {
                    CardId = card.CardId,
                    Identity = identity,
                    CardsOwned = card.CardsOwned,
                    CardsForTrade = card.CardsForTrade
                };

                byId[card.CardId] = row;
                byIdentity[identity] = row;
            }

            return new CollectionSnapshot(byId, byIdentity);
        }
        public static CollectionSnapshot FromRows(IEnumerable<MyCollectionRow> rows)
        {
            var byId = new Dictionary<int, MyCollectionRow>(capacity: 1024);
            var byIdentity = new Dictionary<CollectionIdentity, MyCollectionRow>(capacity: 1024);

            foreach (var row in rows)
            {
                byId[row.CardId] = row;
                byIdentity[row.Identity] = row;
            }

            return new CollectionSnapshot(byId, byIdentity);
        }
    }
}
