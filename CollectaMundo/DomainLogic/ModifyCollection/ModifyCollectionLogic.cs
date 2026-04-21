using CollectaMundo.ApplicationServices.EditCollection.Models;
using CollectaMundo.ApplicationServices.Import.Models;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Import.Models;
using CollectaMundo.DomainLogic.ModifyCollection.Models;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.DomainLogic.Shared.Models;
using CollectaMundo.ViewModels;
using System.Diagnostics;

namespace CollectaMundo.DomainLogic.ModifyCollection
{
    public class ModifyCollectionLogic() : IModifyCollectionLogic
    {
        private static readonly string _defaultLanguage = CollectionCardItemDefaults.GetDefaultString(ImportField.Language);
        public CardSet PrepareCardForList(CardSet selectedCard, CardToAddMetadataDto metadata, bool isEdit)
        {
            if (selectedCard.Core is null)
            {
                throw new InvalidOperationException("CardSet.Core must be set. Use CardSet.FromCore.");
            }

            var clone = CardSet.FromCore(selectedCard.Core);

            // Carry over view-only fields from the source row
            clone.SelectedFinish = selectedCard.SelectedFinish;
            clone.SelectedCondition = selectedCard.SelectedCondition;
            clone.Count = selectedCard.Count;

            // Attach selectable metadata for the editor
            clone.AvailableFinishes = [.. metadata.AvailableFinishes];
            clone.OtherLanguages = NormalizeLanguages(metadata.AvailableLanguages, selectedCard.Language);

            if (isEdit)
            {
                // Preserve the full collection row state when editing
                clone.CardId = selectedCard.CardId;
                clone.CardsOwned = selectedCard.CardsOwned;
                clone.CardsForTrade = selectedCard.CardsForTrade;
                clone.Language = selectedCard.Language!;
                clone.SelectedFinish = selectedCard.SelectedFinish!;
                clone.SelectedCondition = selectedCard.SelectedCondition!;
                clone.SelectedLocationId = selectedCard.SelectedLocationId;
                clone.Comment = selectedCard.Comment;
            }
            else
            {
                // New rows start from collection defaults
                ApplyNewDefaults(clone);
            }

            clone.RecomputeCollectionPrice();
            return clone;
        }
        public CardSet PrepareNewCardWithDefaults(CardSet selectedCard, CardToAddMetadataDto metadata)
        {
            if (selectedCard.Core is null)
            {
                throw new InvalidOperationException("CardSet.Core must be set. Use CardSet.FromCore.");
            }

            var clone = CardSet.FromCore(selectedCard.Core);

            // Copy metadata lists so the edit row owns its own selections
            clone.AvailableFinishes = metadata.AvailableFinishes.ToList();
            clone.OtherLanguages = NormalizeLanguages(metadata.AvailableLanguages, selectedCard.Language);

            ApplyNewDefaults(clone);

            clone.RecomputeCollectionPrice();
            return clone;
        }

        // Helper methods for PrepareCardForList
        private static void ApplyNewDefaults(CardSet clone)
        {
            clone.CardId = null;
            clone.CardsOwned = CollectionCardItemDefaults.GetDefaultInt(ImportField.CardsOwned);
            clone.CardsForTrade = CollectionCardItemDefaults.GetDefaultInt(ImportField.CardsForTrade);
            clone.SelectedCondition = CollectionCardItemDefaults.GetDefaultString(ImportField.Condition);
            clone.SelectedFinish = ChooseDefaultFinish(clone.AvailableFinishes);
            clone.SelectedLocationId = null;
            clone.Comment = null;
            clone.Language = ChooseDefaultLanguage(clone.OtherLanguages);
        }
        private static string? ChooseDefaultFinish(List<string>? finishes)
        {
            if (finishes == null || finishes.Count == 0)
            {
                return null;
            }

            static int Rank(string s) => s switch
            {
                var x when x.Equals("nonfoil", StringComparison.OrdinalIgnoreCase) => 0,
                var x when x.Equals("foil", StringComparison.OrdinalIgnoreCase) => 1,
                var x when x.Equals("etched", StringComparison.OrdinalIgnoreCase) => 2,
                _ => 3
            };

            return finishes
                .OrderBy(Rank)
                .ThenBy(s => s, StringComparer.OrdinalIgnoreCase)
                .First();
        }
        private static string ChooseDefaultLanguage(List<string>? langs)
        {
            if (langs == null || langs.Count == 0)
            {
                return _defaultLanguage;
            }

            var english = langs.FirstOrDefault(l =>
                l.Equals(_defaultLanguage, StringComparison.OrdinalIgnoreCase));

            return english ?? langs[0];
        }
        private static List<string> NormalizeLanguages(IEnumerable<string>? langs, string? primary)
        {
            var list = (langs ?? [])
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Include the current language if it is not already present
            if (!string.IsNullOrWhiteSpace(primary) &&
                !list.Contains(primary, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(primary);
            }

            // Prefer English first, then the current language, then alphabetical
            list.Sort(StringComparer.OrdinalIgnoreCase);
            MoveToFront(list, _defaultLanguage);

            if (!string.Equals(primary, _defaultLanguage, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(primary))
            {
                MoveToFront(list, primary);
            }

            return list;
        }
        private static void MoveToFront(List<string> list, string value)
        {
            var idx = list.FindIndex(s => string.Equals(s, value, StringComparison.OrdinalIgnoreCase));
            if (idx > 0)
            {
                var item = list[idx];
                list.RemoveAt(idx);
                list.Insert(0, item);
            }
        }


        public ModifyBatchPlan PlanBatch(IEnumerable<CardSet> cards, ICollectionSnapshot snapshot, bool isEdit)
        {
            Debug.WriteLine($"[PlanBatch] START isEdit={isEdit}");

            var plan = new ModifyBatchPlan();
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

                var identity = CollectionIdentityFactory.Create(card.Uuid,card.SelectedCondition,card.Language,card.SelectedFinish,card.SelectedLocationId,card.Comment);

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
        // Helper methods for PlanBatch
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
        private static void PlanAdd(CardSet card,CollectionIdentity identity,Dictionary<int, UpdateCommand> updatesByCardId,ModifyBatchPlan plan,Dictionary<CollectionIdentity, CardSet> upsertsByIdentity,Dictionary<CollectionIdentity, WorkingRow> workingByIdentity)
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
        private static void PlanEdit(CardSet card,CollectionIdentity targetIdentity,Dictionary<int, UpdateCommand> updatesByCardId,ModifyBatchPlan plan,HashSet<int> removedIds,Dictionary<CollectionIdentity, CardSet> upsertsByIdentity,Dictionary<int, WorkingRow> workingById,Dictionary<CollectionIdentity, WorkingRow> workingByIdentity)
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
        private static void PlanEditDelete(CardSet card,ModifyBatchPlan plan,HashSet<int> removedIds,Dictionary<CollectionIdentity, CardSet> upsertsByIdentity,Dictionary<int, WorkingRow> workingById,Dictionary<CollectionIdentity, WorkingRow> workingByIdentity)
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



        public void ApplyMyCollectionChanges(IList<CardSet> collection, CollectionChangeSet<CardSet> changes)
        {
            if (collection == null || changes == null)
            {
                return;
            }

            // Remove deleted rows first
            if (changes.RemovedIds.Count > 0)
            {
                for (int i = collection.Count - 1; i >= 0; i--)
                {
                    var card = collection[i];
                    if (card.CardId is int id && changes.RemovedIds.Contains(id))
                    {
                        collection.RemoveAt(i);
                    }
                }
            }

            // Replace existing rows by CardId, otherwise append
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
                        collection[index] = incoming;
                        continue;
                    }
                }

                collection.Add(incoming);
            }
        }

        // Internal class used to track working state during PlanBatch
        private sealed class WorkingRow
        {
            public int CardId { get; set; }
            public CollectionIdentity Identity { get; set; } = default!;
            public int CardsOwned { get; set; }
            public int CardsForTrade { get; set; }
        }
    }
}
