using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.CollectionMutations.Models;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.DomainLogic.Shared.Models;
using System.Diagnostics;

namespace CollectaMundo.DomainLogic.CollectionMutations
{
    public class CollectionMutationsLogic : ICollectionMutationsLogic
    {
        public CollectionMutationPlan PlanIdentityRewriteBatch(IEnumerable<CardSet> cards, ICollectionSnapshot snapshot, bool isEdit)
        {
            Debug.WriteLine($"[PlanBatch] START isEdit={isEdit}");

            var plan = new CollectionMutationPlan();
            var removedIds = new HashSet<int>();
            var upsertsByIdentity = new Dictionary<CollectionIdentity, CardSet>();
            var updatesByCardId = new Dictionary<int, UpdateCommand>();

            var workingById = new Dictionary<int, WorkingRow>();
            var workingByIdentity = new Dictionary<CollectionIdentity, WorkingRow>();

            SeedWorkingState(snapshot, workingById, workingByIdentity);

            foreach (var card in cards)
            {
                if (isEdit && card.CardsOwned == 0)
                {
                    PlanEditDelete(card, plan, removedIds, upsertsByIdentity, workingById, workingByIdentity);
                    continue;
                }

                var identity = CollectionIdentityFactory.Create(card.Uuid, card.SelectedCondition, card.Language, card.SelectedFinish, card.SelectedLocationId, card.Comment);

                if (!isEdit)
                {
                    PlanAdd(card, identity, updatesByCardId, plan, upsertsByIdentity, workingByIdentity);
                    continue;
                }

                PlanEdit(card, identity, updatesByCardId, plan, removedIds, upsertsByIdentity, workingById, workingByIdentity);
            }

            plan.Updates.Clear();
            plan.Updates.AddRange(updatesByCardId.Values);

            plan.ChangeSet = new CollectionChangeSet<CardSet>
            {
                RemovedIds = [.. removedIds],
                AddedOrUpdated = [.. upsertsByIdentity.Values]
            };

            Debug.WriteLine($"[PlanBatch] END Deletes={plan.DeleteIds.Count} Updates={plan.Updates.Count} Inserts={plan.Inserts.Count}");

            return plan;
        }

        // Helper methods for PlanIdentityRewriteBatch
        private static void SeedWorkingState(ICollectionSnapshot snapshot, Dictionary<int, WorkingRow> workingById, Dictionary<CollectionIdentity, WorkingRow> workingByIdentity)
        {
            foreach (var row in snapshot.Rows)
            {
                var working = new WorkingRow
                {
                    CardId = row.CardId,
                    Identity = row.Identity,
                    CardsOwned = row.CardsOwned,
                    CardsForTrade = row.CardsForTrade
                };

                workingById[working.CardId] = working;
                workingByIdentity[working.Identity] = working;
            }
        }
        private static void PlanAdd(CardSet card, CollectionIdentity identity, Dictionary<int, UpdateCommand> updatesByCardId, CollectionMutationPlan plan, Dictionary<CollectionIdentity, CardSet> upsertsByIdentity, Dictionary<CollectionIdentity, WorkingRow> workingByIdentity)
        {
            if (!workingByIdentity.TryGetValue(identity, out var existing))
            {
                plan.Inserts.Add(new InsertCommand(identity, card.CardsOwned, card.CardsForTrade, card));
                upsertsByIdentity[identity] = card;
                return;
            }

            var mergedOwned = existing.CardsOwned + card.CardsOwned;
            var mergedTrade = existing.CardsForTrade + card.CardsForTrade;

            SetUpdate(updatesByCardId, existing.CardId, identity, mergedOwned, mergedTrade);

            existing.CardsOwned = mergedOwned;
            existing.CardsForTrade = mergedTrade;

            card.CardId = existing.CardId;
            card.CardsOwned = mergedOwned;
            card.CardsForTrade = mergedTrade;

            upsertsByIdentity[identity] = card;
        }
        private static void PlanEdit(CardSet card, CollectionIdentity targetIdentity, Dictionary<int, UpdateCommand> updatesByCardId, CollectionMutationPlan plan, HashSet<int> removedIds, Dictionary<CollectionIdentity, CardSet> upsertsByIdentity, Dictionary<int, WorkingRow> workingById, Dictionary<CollectionIdentity, WorkingRow> workingByIdentity)
        {
            var currentId = card.CardId ?? throw new InvalidOperationException("Edit requires CardId");

            if (!workingById.TryGetValue(currentId, out var currentRow))
            {
                throw new InvalidOperationException($"CardId {currentId} not found in working state");
            }

            var currentIdentity = currentRow.Identity;

            if (targetIdentity.Equals(currentIdentity))
            {
                SetUpdate(updatesByCardId, currentId, targetIdentity, card.CardsOwned, card.CardsForTrade);

                currentRow.CardsOwned = card.CardsOwned;
                currentRow.CardsForTrade = card.CardsForTrade;

                upsertsByIdentity[targetIdentity] = card;
                return;
            }

            if (workingByIdentity.TryGetValue(targetIdentity, out var survivor))
            {
                if (survivor.CardId == currentId)
                {
                    SetUpdate(updatesByCardId, currentId, targetIdentity, card.CardsOwned, card.CardsForTrade);

                    currentRow.Identity = targetIdentity;
                    currentRow.CardsOwned = card.CardsOwned;
                    currentRow.CardsForTrade = card.CardsForTrade;

                    workingByIdentity.Remove(currentIdentity);
                    workingByIdentity[targetIdentity] = currentRow;

                    upsertsByIdentity[targetIdentity] = card;
                    return;
                }

                var mergedOwned = survivor.CardsOwned + card.CardsOwned;
                var mergedTrade = survivor.CardsForTrade + card.CardsForTrade;

                plan.DeleteIds.Add(currentId);
                removedIds.Add(currentId);

                // The current row disappears, so any prior update for it is no longer relevant
                updatesByCardId.Remove(currentId);

                SetUpdate(updatesByCardId, survivor.CardId, targetIdentity, mergedOwned, mergedTrade);

                workingById.Remove(currentId);
                workingByIdentity.Remove(currentIdentity);

                survivor.CardsOwned = mergedOwned;
                survivor.CardsForTrade = mergedTrade;

                card.CardId = survivor.CardId;
                card.CardsOwned = mergedOwned;
                card.CardsForTrade = mergedTrade;

                upsertsByIdentity[targetIdentity] = card;
                return;
            }

            SetUpdate(updatesByCardId, currentId, targetIdentity, card.CardsOwned, card.CardsForTrade);

            workingByIdentity.Remove(currentIdentity);

            currentRow.Identity = targetIdentity;
            currentRow.CardsOwned = card.CardsOwned;
            currentRow.CardsForTrade = card.CardsForTrade;

            workingByIdentity[targetIdentity] = currentRow;
            upsertsByIdentity[targetIdentity] = card;
        }
        private static void PlanEditDelete(CardSet card, CollectionMutationPlan plan, HashSet<int> removedIds, Dictionary<CollectionIdentity, CardSet> upsertsByIdentity, Dictionary<int, WorkingRow> workingById, Dictionary<CollectionIdentity, WorkingRow> workingByIdentity)
        {
            var currentId = card.CardId ?? throw new InvalidOperationException("Edit requires CardId");

            if (!workingById.TryGetValue(currentId, out var currentRow))
            {
                throw new InvalidOperationException($"CardId {currentId} not found in working state");
            }

            var currentIdentity = currentRow.Identity;

            plan.DeleteIds.Add(currentId);
            removedIds.Add(currentId);

            workingById.Remove(currentId);
            workingByIdentity.Remove(currentIdentity);
            upsertsByIdentity.Remove(currentIdentity);
        }
        private static void SetUpdate(Dictionary<int, UpdateCommand> updatesByCardId, int cardId, CollectionIdentity identity, int cardsOwned, int cardsForTrade)
        {
            updatesByCardId[cardId] = new UpdateCommand(cardId, identity, cardsOwned, cardsForTrade);
        }

        // Internal class used to track working state during PlanIdentityRewriteBatch
        private sealed class WorkingRow
        {
            public int CardId { get; set; }
            public CollectionIdentity Identity { get; set; } = default!;
            public int CardsOwned { get; set; }
            public int CardsForTrade { get; set; }
        }
    }
}
