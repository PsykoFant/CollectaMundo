using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.CollectionMutations.Models;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.DomainLogic.Shared.Models;
using System.Diagnostics;

namespace CollectaMundo.DomainLogic.CollectionMutations
{
    public class CollectionMutationsLogic : ICollectionMutationsLogic
    {
        public CollectionMutationPlan PlanIdentityRewriteBatch(IEnumerable<CardSet> cards, ICollectionSnapshot snapshot)
        {
            var plan = new CollectionMutationPlan();
            var removedIds = new HashSet<int>();
            var upsertsByIdentity = new Dictionary<CollectionIdentity, CardSet>();
            var updatesByCardId = new Dictionary<int, UpdateCommand>();

            var workingById = new Dictionary<int, WorkingRow>();
            var workingByIdentity = new Dictionary<CollectionIdentity, WorkingRow>();
            var insertsByIdentity = new Dictionary<CollectionIdentity, InsertCommand>();

            SeedWorkingState(snapshot, workingById, workingByIdentity);

            foreach (var card in cards)
            {
                var isExistingRow = card.CardId is not null;

                if (isExistingRow && card.CardsOwned == 0)
                {
                    PlanEditDelete(card, plan, removedIds, upsertsByIdentity, workingById, workingByIdentity);
                    continue;
                }

                if (!isExistingRow && card.CardsOwned == 0)
                {
                    continue;
                }

                var identity = CollectionIdentityFactory.Create(card.Uuid, card.SelectedCondition, card.Language, card.SelectedFinish, card.SelectedLocationId, card.Comment);

                if (!isExistingRow)
                {
                    PlanAdd(card, identity, updatesByCardId, plan, upsertsByIdentity, workingByIdentity, insertsByIdentity);
                    continue;
                }

                PlanEdit(card, identity, updatesByCardId, plan, removedIds, upsertsByIdentity, workingById, workingByIdentity, insertsByIdentity);

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
        private static void PlanAdd(CardSet card, CollectionIdentity identity, Dictionary<int, UpdateCommand> updatesByCardId, CollectionMutationPlan plan, Dictionary<CollectionIdentity, CardSet> upsertsByIdentity, Dictionary<CollectionIdentity, WorkingRow> workingByIdentity, Dictionary<CollectionIdentity, InsertCommand> insertsByIdentity)
        {
            // Same new identity was already planned for insert in this batch, so collapse into one insert.
            if (insertsByIdentity.TryGetValue(identity, out var existingInsert))
            {
                var mergedOwned = existingInsert.CardsOwned + card.CardsOwned;
                var mergedTrade = existingInsert.CardsForTrade + card.CardsForTrade;

                var replacement = new InsertCommand(
                    identity,
                    mergedOwned,
                    mergedTrade,
                    card);

                var index = plan.Inserts.IndexOf(existingInsert);
                if (index < 0)
                {
                    throw new InvalidOperationException(
                        $"Planned insert for identity '{identity}' was tracked but not found in plan.");
                }

                plan.Inserts[index] = replacement;
                insertsByIdentity[identity] = replacement;

                card.CardsOwned = mergedOwned;
                card.CardsForTrade = mergedTrade;

                upsertsByIdentity[identity] = card;
                return;
            }

            // Identity already exists in the collection snapshot, so adding becomes an additive update.
            if (workingByIdentity.TryGetValue(identity, out var existing))
            {
                var mergedOwned = existing.CardsOwned + card.CardsOwned;
                var mergedTrade = existing.CardsForTrade + card.CardsForTrade;

                SetUpdate(updatesByCardId, existing.CardId, identity, mergedOwned, mergedTrade);

                existing.CardsOwned = mergedOwned;
                existing.CardsForTrade = mergedTrade;

                card.CardId = existing.CardId;
                card.CardsOwned = mergedOwned;
                card.CardsForTrade = mergedTrade;

                upsertsByIdentity[identity] = card;
                return;
            }

            // Brand-new identity: schedule one insert and track it so later identical rows can collapse into it.
            var insert = new InsertCommand(
                identity,
                card.CardsOwned,
                card.CardsForTrade,
                card);

            plan.Inserts.Add(insert);
            insertsByIdentity[identity] = insert;

            upsertsByIdentity[identity] = card;
        }
        private static void PlanEdit(CardSet card, CollectionIdentity targetIdentity, Dictionary<int, UpdateCommand> updatesByCardId, CollectionMutationPlan plan, HashSet<int> removedIds, Dictionary<CollectionIdentity, CardSet> upsertsByIdentity, Dictionary<int, WorkingRow> workingById, Dictionary<CollectionIdentity, WorkingRow> workingByIdentity, Dictionary<CollectionIdentity, InsertCommand> insertsByIdentity)
        {
            var currentId = card.CardId ?? throw new InvalidOperationException("Edit requires CardId");

            if (!workingById.TryGetValue(currentId, out var currentRow))
            {
                throw new InvalidOperationException($"CardId {currentId} not found in working state");
            }

            var currentIdentity = currentRow.Identity;

            // Identity did not change; only quantities need updating.
            if (targetIdentity.Equals(currentIdentity))
            {
                SetUpdate(updatesByCardId, currentId, targetIdentity, card.CardsOwned, card.CardsForTrade);

                currentRow.CardsOwned = card.CardsOwned;
                currentRow.CardsForTrade = card.CardsForTrade;

                upsertsByIdentity[targetIdentity] = card;
                return;
            }

            // Target identity is currently a planned insert from this same batch.
            // Cancel that insert and fold its quantities into this existing row update.
            if (insertsByIdentity.TryGetValue(targetIdentity, out var plannedInsert))
            {
                var mergedOwned = card.CardsOwned + plannedInsert.CardsOwned;
                var mergedTrade = card.CardsForTrade + plannedInsert.CardsForTrade;

                var insertIndex = plan.Inserts.IndexOf(plannedInsert);
                if (insertIndex < 0)
                {
                    throw new InvalidOperationException(
                        $"Planned insert for identity '{targetIdentity}' was tracked but not found in plan.");
                }

                plan.Inserts.RemoveAt(insertIndex);
                insertsByIdentity.Remove(targetIdentity);

                SetUpdate(updatesByCardId, currentId, targetIdentity, mergedOwned, mergedTrade);

                workingByIdentity.Remove(currentIdentity);

                currentRow.Identity = targetIdentity;
                currentRow.CardsOwned = mergedOwned;
                currentRow.CardsForTrade = mergedTrade;

                workingByIdentity[targetIdentity] = currentRow;

                card.CardsOwned = mergedOwned;
                card.CardsForTrade = mergedTrade;

                upsertsByIdentity[targetIdentity] = card;
                return;
            }

            // Target identity already exists in the working collection state, so this edit merges into that survivor.
            if (workingByIdentity.TryGetValue(targetIdentity, out var survivor))
            {
                // Defensive branch: the target identity maps back to the same row.
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

                // Current row is absorbed by the survivor row.
                plan.DeleteIds.Add(currentId);
                removedIds.Add(currentId);

                // The current row disappears, so any prior update for it is no longer relevant.
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

            // Identity changed, but no collision exists; update this row in place to the new identity.
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

            // Owned count zero means delete this existing collection row.
            plan.DeleteIds.Add(currentId);
            removedIds.Add(currentId);

            // Remove the row from working state so later rows in the same batch plan against the updated collection.
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
