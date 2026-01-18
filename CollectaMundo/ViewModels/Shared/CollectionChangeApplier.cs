using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Import.Models;
using CollectaMundo.DomainLogic.Shared;

namespace CollectaMundo.ViewModels.Shared
{
    public sealed class CollectionChangeApplier : ICollectionChangeApplier<CardSet>
    {
        public void Apply(IList<CardSet> collection, CollectionChangeSet<CardSet> changes)
        {
            if (collection == null || changes == null)
            {
                return;
            }

            // Remove
            if (changes.RemovedIds.Count > 0)
            {
                // iterate backwards to avoid index issues on IList
                for (int i = collection.Count - 1; i >= 0; i--)
                {
                    var c = collection[i];
                    if (c.CardId is int id && changes.RemovedIds.Contains(id))
                    {
                        collection.RemoveAt(i);
                    }
                }
            }

            // Upsert
            foreach (var incoming in changes.AddedOrUpdated)
            {
                if (incoming.CardId is int cardId)
                {
                    var index = -1;
                    for (int i = 0; i < collection.Count; i++)
                    {
                        if (collection[i].CardId == cardId)
                        {
                            index = i;
                            break;
                        }
                    }

                    if (index >= 0)
                    {
                        collection[index] = incoming; // works for ObservableCollection too
                        continue;
                    }
                }

                collection.Add(incoming);
            }
        }
        public void ApplyImportUpserts(IList<CardSet> collection, IReadOnlyList<CollectionUpsertItem> upserts)
        {
            if (upserts.Count == 0)
            {
                return;
            }

            foreach (var upsert in upserts)
            {
                // Match by business key
                var matches = collection
                    .Where(c =>
                        string.Equals(c.Uuid, upsert.Uuid, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(c.Language, upsert.Language, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(c.SelectedFinish, upsert.Finish, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(c.SelectedCondition, upsert.Condition, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (matches.Count == 0)
                {
                    // ADD
                    var card = new CardSet
                    {
                        Uuid = upsert.Uuid,
                        Language = upsert.Language,
                        SelectedFinish = upsert.Finish,
                        SelectedCondition = upsert.Condition,
                        CardsOwned = upsert.CardsOwned,
                        CardsForTrade = upsert.CardsForTrade
                    };

                    card.RecomputeCollectionPrice();
                    collection.Add(card);
                }
                else
                {
                    // UPDATE survivor (first is deterministic)
                    var survivor = matches[0];

                    survivor.CardsOwned = upsert.CardsOwned;
                    survivor.CardsForTrade = upsert.CardsForTrade;
                    survivor.RecomputeCollectionPrice();

                    // REMOVE duplicates
                    foreach (var dup in matches.Skip(1))
                    {
                        collection.Remove(dup);
                    }
                }
            }
        }
    }
}
