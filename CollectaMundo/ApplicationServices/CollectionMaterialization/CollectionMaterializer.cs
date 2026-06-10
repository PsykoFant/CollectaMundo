using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Shared.Models;

namespace CollectaMundo.ApplicationServices.CollectionMaterialization
{
    public sealed class CollectionMaterializer : ICollectionMaterializer
    {
        public CollectionCard MaterializeFromRow(MyCollectionRow row, IReadOnlyDictionary<string, PrintingCard> printingByUuid)
        {
            if (!printingByUuid.TryGetValue(row.Identity.Uuid, out var printing))
            {
                throw new InvalidOperationException($"Cannot materialize collection card. Printing not found for UUID '{row.Identity.Uuid}'.");
            }

            return MaterializeFromPrintingAndRow(printing, row);
        }
        public IReadOnlyList<CollectionCard> MaterializeRows(IEnumerable<MyCollectionRow> rows, IReadOnlyDictionary<string, PrintingCard> printingByUuid)
        {
            return
            [
                .. rows.Select(row => MaterializeFromRow(row, printingByUuid))
            ];
        }
        public CollectionCard MergeIntoExisting(CollectionCard existing, CollectionCard incoming)
        {
            if (IsHydrated(incoming))
            {
                return incoming;
            }

            existing.CardsOwned = incoming.CardsOwned;
            existing.CardsForTrade = incoming.CardsForTrade;
            existing.SelectedCondition = incoming.SelectedCondition;
            existing.SelectedFinish = incoming.SelectedFinish;
            existing.SelectedLocationId = incoming.SelectedLocationId;
            existing.Comment = incoming.Comment;

            existing.RecomputeCollectionPrice();

            return existing;
        }
        private static bool IsHydrated(CollectionCard card)
        {
            return card.Printing is not null
                && !string.IsNullOrWhiteSpace(card.Uuid)
                && !string.IsNullOrWhiteSpace(card.Name);
        }
        private static CollectionCard MaterializeFromPrintingAndRow(PrintingCard printing, MyCollectionRow row)
        {
            var identity = row.Identity;

            var card = new CollectionCard
            {
                Printing = printing,
                CardId = row.CardId,
                CardsOwned = row.CardsOwned,
                CardsForTrade = row.CardsForTrade,
                SelectedCondition = identity.Condition,
                SelectedFinish = identity.Finish,
                SelectedLocationId = identity.LocationId,
                Comment = identity.Comment
            };

            card.RecomputeCollectionPrice();

            return card;
        }
    }
}
