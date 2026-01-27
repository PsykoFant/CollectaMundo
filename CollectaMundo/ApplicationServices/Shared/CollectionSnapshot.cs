using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Shared;

namespace CollectaMundo.ApplicationServices.Shared
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

        public bool TryGetById(int cardId, out MyCollectionRow row) => _byId.TryGetValue(cardId, out row!);

        public bool TryGetByIdentity(CollectionIdentity identity, out MyCollectionRow row) => _byIdentity.TryGetValue(identity, out row!);

        public static CollectionSnapshot From(IEnumerable<CardSet> cards)
        {
            var byId = new Dictionary<int, MyCollectionRow>(capacity: 1024);
            var byIdentity = new Dictionary<CollectionIdentity, MyCollectionRow>(capacity: 1024);

            foreach (var card in cards)
            {
                // We rely on the invariant: in-memory collection mirrors DB
                if (card.CardId is not int cardId)
                {
                    continue; // or throw, depending on how strict you want to be
                }

                var identity = new CollectionIdentity(
                    card.Uuid ?? throw new InvalidOperationException("Uuid is required"),
                    card.SelectedCondition ?? throw new InvalidOperationException("Condition is required"),
                    card.Language ?? throw new InvalidOperationException("Language is required"),
                    card.SelectedFinish ?? throw new InvalidOperationException("Finish is required"));

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
