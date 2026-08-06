using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Shared.Factories;
using CollectaMundo.DomainLogic.Shared.Models;
using CollectaMundo.Infrastructure.Shared.Models;

namespace CollectaMundo.DomainLogic.Shared.CollectionSnapshot
{
    public static class CollectionSnapshotFactory
    {
        public static CollectionIdentitySnapshot CreateIdentitySnapshot(IEnumerable<CollectionCardDbRow> rows)
        {
            ArgumentNullException.ThrowIfNull(rows);

            var rowList = rows.ToList();
            var byId = rowList.ToDictionary(row => row.CardId);
            var byIdentity = rowList.ToDictionary(row => row.Identity);

            return new CollectionIdentitySnapshot(byId, byIdentity, rowList);
        }
        public static CollectionIdentitySnapshot CreateIdentitySnapshot(IEnumerable<CollectionCard> cards)
        {
            ArgumentNullException.ThrowIfNull(cards);

            var rows = cards.Select(card =>
            {
                var identity = CollectionIdentityFactory.Create(
                    card.Uuid,
                    card.SelectedCondition,
                    card.Language,
                    card.SelectedFinish,
                    card.SelectedLocationId,
                    card.Comment);

                return new CollectionCardDbRow
                {
                    CardId = card.CardId,
                    Identity = identity,
                    CardsOwned = card.CardsOwned,
                    CardsForTrade = card.CardsForTrade
                };
            });

            return CreateIdentitySnapshot(rows);
        }

        public static CollectionQuantitySnapshot CreateQuantitySnapshot(IEnumerable<CollectionCard> cards)
        {
            ArgumentNullException.ThrowIfNull(cards);

            var ownedByOracleId = new Dictionary<string, int>(capacity: 1024, comparer: StringComparer.OrdinalIgnoreCase);

            var allocatedByOracleAndLocation = new Dictionary<OracleLocationIdentity, int>();

            foreach (var card in cards)
            {
                var oracleId = card.Oracle.ScryfallOracleId;

                if (string.IsNullOrWhiteSpace(oracleId))
                {
                    continue;
                }

                ownedByOracleId[oracleId] = ownedByOracleId.GetValueOrDefault(oracleId) + card.CardsOwned;

                if (card.SelectedLocationId is not int locationId)
                {
                    continue;
                }

                var key = new OracleLocationIdentity(card.Oracle.ScryfallOracleId,locationId);

                allocatedByOracleAndLocation[key] = allocatedByOracleAndLocation.GetValueOrDefault(key) + card.CardsOwned;
            }

            return new CollectionQuantitySnapshot(ownedByOracleId, allocatedByOracleAndLocation);
        }
    }
}
