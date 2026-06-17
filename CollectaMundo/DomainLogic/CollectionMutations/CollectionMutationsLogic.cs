using CollectaMundo.DomainLogic.CollectionMutations.Models;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.DomainLogic.Shared.Factories;
using CollectaMundo.DomainLogic.Shared.Models;
using CollectaMundo.Infrastructure.Shared.Models;

namespace CollectaMundo.DomainLogic.CollectionMutations
{
    public class CollectionMutationsLogic : ICollectionMutationsLogic
    {
        public CollectionMutationPlan PlanIdentityRewriteBatch(IEnumerable<CollectionCardDraft> cards, ICollectionSnapshot snapshot)
        {
            var plan = new CollectionMutationPlan();
            var removedIds = new HashSet<int>();
            var upsertsByIdentity = plan.UpsertsByIdentity;
            var updatesByCardId = new Dictionary<int, UpdateMutation>();

            var workingById = new Dictionary<int, WorkingRow>();
            var workingByIdentity = new Dictionary<CollectionIdentity, WorkingRow>();
            var insertsByIdentity = new Dictionary<CollectionIdentity, InsertMutation>();

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
        private static void PlanAdd(
            CollectionCardDraft card,
            CollectionIdentity identity,
            Dictionary<int, UpdateMutation> updatesByCardId,
            CollectionMutationPlan plan,
            Dictionary<CollectionIdentity, CollectionCardDbRow> upsertsByIdentity,
            Dictionary<CollectionIdentity, WorkingRow> workingByIdentity,
            Dictionary<CollectionIdentity, InsertMutation> insertsByIdentity)
        {
            if (insertsByIdentity.TryGetValue(identity, out var existingInsert))
            {
                var mergedOwned = existingInsert.CardsOwned + card.CardsOwned;
                var mergedTrade = existingInsert.CardsForTrade + card.CardsForTrade;

                var replacement = new InsertMutation(identity, mergedOwned, mergedTrade, card);

                var index = plan.Inserts.IndexOf(existingInsert);
                if (index < 0)
                {
                    throw new InvalidOperationException($"Planned insert for identity '{identity}' was tracked but not found in plan.");
                }

                plan.Inserts[index] = replacement;
                insertsByIdentity[identity] = replacement;

                upsertsByIdentity[identity] = ToRow(0, identity, mergedOwned, mergedTrade);
                return;
            }

            if (workingByIdentity.TryGetValue(identity, out var existing))
            {
                var mergedOwned = existing.CardsOwned + card.CardsOwned;
                var mergedTrade = existing.CardsForTrade + card.CardsForTrade;

                SetUpdate(updatesByCardId, existing.CardId, identity, mergedOwned, mergedTrade);

                existing.CardsOwned = mergedOwned;
                existing.CardsForTrade = mergedTrade;

                upsertsByIdentity[identity] = ToRow(existing.CardId, identity, mergedOwned, mergedTrade);
                return;
            }

            var insert = new InsertMutation(identity, card.CardsOwned, card.CardsForTrade, card);

            plan.Inserts.Add(insert);
            insertsByIdentity[identity] = insert;

            upsertsByIdentity[identity] = ToRow(0, identity, card.CardsOwned, card.CardsForTrade);
        }
        private static void PlanEdit(
            CollectionCardDraft card,
            CollectionIdentity targetIdentity,
            Dictionary<int, UpdateMutation> updatesByCardId,
            CollectionMutationPlan plan,
            HashSet<int> removedIds,
            Dictionary<CollectionIdentity, CollectionCardDbRow> upsertsByIdentity,
            Dictionary<int, WorkingRow> workingById,
            Dictionary<CollectionIdentity, WorkingRow> workingByIdentity,
            Dictionary<CollectionIdentity, InsertMutation> insertsByIdentity)
        {
            var currentId = card.CardId
                ?? throw new InvalidOperationException("Edit requires CardId.");

            if (!workingById.TryGetValue(currentId, out var currentRow))
            {
                throw new InvalidOperationException($"CardId {currentId} not found in working state.");
            }

            var currentIdentity = currentRow.Identity;

            // Branch 1:
            // Identity did not change. Only quantity fields may have changed.
            if (targetIdentity.Equals(currentIdentity))
            {
                SetUpdate(updatesByCardId, currentId, targetIdentity, card.CardsOwned, card.CardsForTrade);

                currentRow.CardsOwned = card.CardsOwned;
                currentRow.CardsForTrade = card.CardsForTrade;

                upsertsByIdentity[targetIdentity] = ToRow(currentId, targetIdentity, card.CardsOwned, card.CardsForTrade);

                return;
            }

            // Branch 2:
            // Target identity is currently a planned insert from this same batch.
            // Cancel that insert and fold its quantities into this existing row update.
            if (insertsByIdentity.TryGetValue(targetIdentity, out var plannedInsert))
            {
                var mergedOwned = card.CardsOwned + plannedInsert.CardsOwned;
                var mergedTrade = card.CardsForTrade + plannedInsert.CardsForTrade;

                if (!plan.Inserts.Remove(plannedInsert))
                {
                    throw new InvalidOperationException($"Planned insert for identity '{targetIdentity}' was tracked but not found in plan.");
                }

                insertsByIdentity.Remove(targetIdentity);

                SetUpdate(updatesByCardId, currentId, targetIdentity, mergedOwned, mergedTrade);

                workingByIdentity.Remove(currentIdentity);

                currentRow.Identity = targetIdentity;
                currentRow.CardsOwned = mergedOwned;
                currentRow.CardsForTrade = mergedTrade;

                workingByIdentity[targetIdentity] = currentRow;

                card.CardsOwned = mergedOwned;
                card.CardsForTrade = mergedTrade;

                upsertsByIdentity.Remove(currentIdentity);
                upsertsByIdentity[targetIdentity] = ToRow(currentId, targetIdentity, mergedOwned, mergedTrade);

                return;
            }

            // Branch 3:
            // Target identity already exists in the working collection state.
            // This edit either updates the same row defensively, or merges the current row
            // into the existing survivor row and deletes the current row.
            if (workingByIdentity.TryGetValue(targetIdentity, out var survivor))
            {
                // Branch 3a:
                // Defensive case: target identity maps back to the same row.
                if (survivor.CardId == currentId)
                {
                    SetUpdate(updatesByCardId, currentId, targetIdentity, card.CardsOwned, card.CardsForTrade);

                    currentRow.Identity = targetIdentity;
                    currentRow.CardsOwned = card.CardsOwned;
                    currentRow.CardsForTrade = card.CardsForTrade;

                    workingByIdentity.Remove(currentIdentity);
                    workingByIdentity[targetIdentity] = currentRow;

                    upsertsByIdentity.Remove(currentIdentity);
                    upsertsByIdentity[targetIdentity] = ToRow(currentId, targetIdentity, card.CardsOwned, card.CardsForTrade);

                    return;
                }

                // Branch 3b:
                // Target identity belongs to another row. Current row is absorbed into
                // the survivor row, so current row must be deleted and survivor updated.
                var mergedOwned = survivor.CardsOwned + card.CardsOwned;
                var mergedTrade = survivor.CardsForTrade + card.CardsForTrade;

                plan.DeleteIds.Add(currentId);
                removedIds.Add(currentId);

                // The current row disappears, so any prior update for it is no longer valid.
                updatesByCardId.Remove(currentId);

                SetUpdate(updatesByCardId, survivor.CardId, targetIdentity, mergedOwned, mergedTrade);

                workingById.Remove(currentId);
                workingByIdentity.Remove(currentIdentity);
                upsertsByIdentity.Remove(currentIdentity);

                survivor.CardsOwned = mergedOwned;
                survivor.CardsForTrade = mergedTrade;

                card.CardId = survivor.CardId;
                card.CardsOwned = mergedOwned;
                card.CardsForTrade = mergedTrade;

                upsertsByIdentity[targetIdentity] = ToRow(survivor.CardId, targetIdentity, mergedOwned, mergedTrade);

                return;
            }

            // Branch 4:
            // Identity changed, but no collision exists.
            // Update this row in place from current identity to target identity.
            SetUpdate(updatesByCardId, currentId, targetIdentity, card.CardsOwned, card.CardsForTrade);

            workingByIdentity.Remove(currentIdentity);

            currentRow.Identity = targetIdentity;
            currentRow.CardsOwned = card.CardsOwned;
            currentRow.CardsForTrade = card.CardsForTrade;

            workingByIdentity[targetIdentity] = currentRow;

            upsertsByIdentity.Remove(currentIdentity);
            upsertsByIdentity[targetIdentity] = ToRow(currentId, targetIdentity, card.CardsOwned, card.CardsForTrade);
        }
        private static void PlanEditDelete(
            CollectionCardDraft card,
            CollectionMutationPlan plan,
            HashSet<int> removedIds,
            Dictionary<CollectionIdentity, CollectionCardDbRow> upsertsByIdentity,
            Dictionary<int, WorkingRow> workingById,
            Dictionary<CollectionIdentity, WorkingRow> workingByIdentity)
        {
            var currentId = card.CardId ?? throw new InvalidOperationException("Edit delete requires CardId.");

            if (!workingById.TryGetValue(currentId, out var currentRow))
            {
                throw new InvalidOperationException($"CardId {currentId} not found in working state.");
            }

            var currentIdentity = currentRow.Identity;

            plan.DeleteIds.Add(currentId);
            removedIds.Add(currentId);

            workingById.Remove(currentId);
            workingByIdentity.Remove(currentIdentity);
            upsertsByIdentity.Remove(currentIdentity);
        }
        private static void SetUpdate(Dictionary<int, UpdateMutation> updatesByCardId, int cardId, CollectionIdentity identity, int cardsOwned, int cardsForTrade)
        {
            updatesByCardId[cardId] = new UpdateMutation(cardId, identity, cardsOwned, cardsForTrade);
        }
        private static CollectionCardDbRow ToRow(int cardId, CollectionIdentity identity, int cardsOwned, int cardsForTrade)
        {
            return new CollectionCardDbRow
            {
                CardId = cardId,
                Identity = identity,
                CardsOwned = cardsOwned,
                CardsForTrade = cardsForTrade
            };
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
