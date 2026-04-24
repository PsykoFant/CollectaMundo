using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CollectaMundo.ApplicationServices.CollectionMaterialization
{
    public sealed class CollectionMaterializer : ICollectionMaterializer
    {
        public CardSet MaterializeFromRow(MyCollectionRow row,IReadOnlyDictionary<string, CardCore> coreByUuid)
        {
            if (!coreByUuid.TryGetValue(row.Identity.Uuid, out var core))
            {
                throw new InvalidOperationException($"Cannot materialize collection card. Core not found for UUID '{row.Identity.Uuid}'.");
            }

            return MaterializeFromCoreAndRow(core, row);
        }
        public IReadOnlyList<CardSet> MaterializeRows(IEnumerable<MyCollectionRow> rows,IReadOnlyDictionary<string, CardCore> coreByUuid)
        {
            return
            [
                .. rows.Select(row => MaterializeFromRow(row, coreByUuid))
            ];
        }
        public CardSet MergeIntoExisting(CardSet existing, CardSet incoming)
        {
            if (IsHydrated(incoming))
            {
                return incoming;
            }

            existing.CardId = incoming.CardId;
            existing.CardsOwned = incoming.CardsOwned;
            existing.CardsForTrade = incoming.CardsForTrade;
            existing.SelectedCondition = incoming.SelectedCondition;
            existing.Language = incoming.Language;
            existing.SelectedFinish = incoming.SelectedFinish;
            existing.SelectedLocationId = incoming.SelectedLocationId;
            existing.Comment = incoming.Comment;

            existing.RecomputeCollectionPrice();

            return existing;
        }
        private static bool IsHydrated(CardSet card)
        {
            return card.Core is not null
                && !string.IsNullOrWhiteSpace(card.Uuid)
                && !string.IsNullOrWhiteSpace(card.Name);
        }
        private static CardSet MaterializeFromCoreAndRow(CardCore core, MyCollectionRow row)
        {
            var identity = row.Identity;

            var card = CardSet.FromCore(core);

            card.CardId = row.CardId;
            card.CardsOwned = row.CardsOwned;
            card.CardsForTrade = row.CardsForTrade;
            card.SelectedCondition = identity.Condition;
            card.Language = identity.Language ?? core.Language;
            card.SelectedFinish = identity.Finish;
            card.SelectedLocationId = identity.LocationId;
            card.Comment = identity.Comment;

            card.RecomputeCollectionPrice();

            return card;
        }
    }
}
