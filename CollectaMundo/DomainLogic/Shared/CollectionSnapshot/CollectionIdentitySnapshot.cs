using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Shared.Factories;
using CollectaMundo.DomainLogic.Shared.Models;
using CollectaMundo.Infrastructure.Shared.Models;

namespace CollectaMundo.DomainLogic.Shared.CollectionSnapshot
{
    public sealed class CollectionIdentitySnapshot : ICollectionIdentitySnapshot
    {
        private readonly Dictionary<int, CollectionCardDbRow> _byId;
        private readonly Dictionary<CollectionIdentity, CollectionCardDbRow> _byIdentity;
        private CollectionIdentitySnapshot(Dictionary<int, CollectionCardDbRow> byId, Dictionary<CollectionIdentity, CollectionCardDbRow> byIdentity)
        {
            _byId = byId;
            _byIdentity = byIdentity;
        }
        public IReadOnlyCollection<CollectionCardDbRow> Rows => _byId.Values;
        public bool TryGetById(int cardId, out CollectionCardDbRow row) => _byId.TryGetValue(cardId, out row!);
        public bool TryGetByIdentity(CollectionIdentity identity, out CollectionCardDbRow row) => _byIdentity.TryGetValue(identity, out row!);
        public static CollectionIdentitySnapshot From(IEnumerable<CollectionCard> cards)
        {
            var byId = new Dictionary<int, CollectionCardDbRow>(capacity: 1024);
            var byIdentity = new Dictionary<CollectionIdentity, CollectionCardDbRow>(capacity: 1024);

            foreach (var card in cards)
            {
                var identity = CollectionIdentityFactory.Create(
                    card.Uuid,
                    card.SelectedCondition,
                    card.Language,
                    card.SelectedFinish,
                    card.SelectedLocationId,
                    card.Comment);

                var row = new CollectionCardDbRow
                {
                    CardId = card.CardId,
                    Identity = identity,
                    CardsOwned = card.CardsOwned,
                    CardsForTrade = card.CardsForTrade
                };

                byId[card.CardId] = row;
                byIdentity[identity] = row;
            }

            return new CollectionIdentitySnapshot(byId, byIdentity);
        }
        public static CollectionIdentitySnapshot FromRows(IEnumerable<CollectionCardDbRow> rows)
        {
            var byId = new Dictionary<int, CollectionCardDbRow>(capacity: 1024);
            var byIdentity = new Dictionary<CollectionIdentity, CollectionCardDbRow>(capacity: 1024);

            foreach (var row in rows)
            {
                byId[row.CardId] = row;
                byIdentity[row.Identity] = row;
            }

            return new CollectionIdentitySnapshot(byId, byIdentity);
        }
    }
}
