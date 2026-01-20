using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.EditCollection.Models;
using CollectaMundo.DomainLogic.Import.Models;
using CollectaMundo.DomainLogic.Shared;
using System.Data.SQLite;

namespace CollectaMundo.DomainLogic.EditCollection
{
    public class EditCollectionLogic() : IEditCollectionLogic
    {
        private static readonly string _defaultLanguage = CollectionCardItemDefaults.GetDefaultString(ImportField.Language);
        public async Task<CardSet> PrepareCardForListAsync(CardSet selectedCard, bool isEdit, SQLiteConnection connection)
        {
            var clone = await CloneWithMetadataHelperAsync(selectedCard, connection);

            if (isEdit)
            {
                // carry forward existing collection fields
                clone.CardId = selectedCard.CardId;
                clone.CardsOwned = selectedCard.CardsOwned;
                clone.CardsForTrade = selectedCard.CardsForTrade;
                clone.SelectedCondition = selectedCard.SelectedCondition!;
                clone.SelectedFinish = selectedCard.SelectedFinish!;
                clone.Language = selectedCard.Language!;

                clone.RecomputeCollectionPrice(); // raises PropertyChanged for CardInCollectionPrice

            }
            else
            {
                ApplyNewDefaults(clone);
            }

            return clone;
        }
        public async Task<CardSet> PrepareNewCardWithDefaultsAsync(CardSet selectedCard, SQLiteConnection connection)
        {
            var clone = await CloneWithMetadataHelperAsync(selectedCard, connection);
            ApplyNewDefaults(clone);
            return clone;
        }
        private static void ApplyNewDefaults(CardSet clone)
        {
            clone.CardId = null;
            clone.CardsOwned = CollectionCardItemDefaults.GetDefaultInt(ImportField.CardsOwned);
            clone.CardsForTrade = CollectionCardItemDefaults.GetDefaultInt(ImportField.CardsForTrade);
            clone.SelectedCondition = CollectionCardItemDefaults.GetDefaultString(ImportField.Condition);
            clone.SelectedFinish = ChooseDefaultFinish(clone.AvailableFinishes);

            // prefer English; else first; else "English"
            clone.Language = ChooseDefaultLanguage(clone.OtherLanguages);
        }
        private static string? ChooseDefaultFinish(IList<string>? finishes)
        {
            if (finishes == null || finishes.Count == 0)
            {
                return null;
            }

            static int Rank(string s) => s switch
            {
                // adjust to your canonical strings
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
        private static string ChooseDefaultLanguage(IList<string>? langs)
        {
            if (langs == null || langs.Count == 0)
            {
                return _defaultLanguage;
            }

            var english = langs.FirstOrDefault(l => l.Equals(_defaultLanguage, StringComparison.OrdinalIgnoreCase));
            return english ?? langs[0];
        }
        private static List<string> NormalizeLanguages(IEnumerable<string>? langs, string? primary)
        {
            var list = (langs ?? [])
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // If we have a primary language from the card itself and it's not in the list, include it
            if (!string.IsNullOrWhiteSpace(primary) &&
                !list.Contains(primary, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(primary);
            }

            // Sort with English first (if present), then primary (if not English), then alphabetical
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
                var v = list[idx];
                list.RemoveAt(idx);
                list.Insert(0, v);
            }
        }
        private async Task<CardSet> CloneWithMetadataHelperAsync(CardSet src, SQLiteConnection connection)
        {
            if (src?.Uuid == null)
            {
                throw new ArgumentException("UUID cannot be null", nameof(src));
            }

            // fetch just once
            var finishes = await _repo.FetchFinishesForCardAsync(src.Uuid, connection);
            var languages = await _repo.FetchLanguagesForCardAsync(src.Uuid, connection);

            // Require Core in the new flow to avoid silent inconsistencies.
            // If you really want the fallback, keep it — but log loudly.
            CardSet clone;
            if (src.Core != null)
            {
                clone = CardSet.FromCore(src.Core);
            }
            else
            {
                // Strong fail is safer in the refactored world:
                throw new InvalidOperationException("CardSet.Core must be set. Use CardSet.FromCore to create instances.");
            }

            // carry over view-only fields if needed
            clone.SelectedFinish = src.SelectedFinish;
            clone.SelectedCondition = src.SelectedCondition;
            clone.Count = src.Count;

            // Attach lookup lists (never null)
            clone.AvailableFinishes = finishes ?? [];

            // Distinct, English-first normalization; include src.Language as secondary if present
            clone.OtherLanguages = NormalizeLanguages(languages, src.Language) ?? [];

            clone.RecomputeCollectionPrice();
            return clone;
        }



        // Save a card and return the changes to viewmodel
        public EditBatchPlan PlanBatch(IEnumerable<CardSet> cards, ICollectionSnapshot snapshot, bool isEdit)
        {
            var plan = new EditBatchPlan();

            var removedIds = new HashSet<int>();
            var upsertsByIdentity = new Dictionary<CollectionIdentity, CardSet>();

            foreach (var card in cards)
            {
                var identity = GetIdentity(card);

                // -------- EDIT: deletion-by-zero --------
                if (isEdit && card.CardsOwned == 0)
                {
                    var id = card.CardId ?? throw new InvalidOperationException("Edit requires CardId");
                    plan.DeleteIds.Add(id);
                    removedIds.Add(id);
                    continue;
                }

                // Lookups
                snapshot.TryGetByIdentity(identity, out var existingByIdentity);
                snapshot.TryGetById(card.CardId ?? -1, out var existingById);

                // -------- ADD --------
                if (!isEdit)
                {
                    if (existingByIdentity is null)
                    {
                        // Pure insert
                        plan.Inserts.Add(new InsertCommand(identity, card.CardsOwned, card.CardsForTrade));

                        upsertsByIdentity[identity] = card;
                    }
                    else
                    {
                        // Merge into existing row
                        var mergedOwned = existingByIdentity.CardsOwned + card.CardsOwned;
                        var mergedTrade = existingByIdentity.CardsForTrade + card.CardsForTrade;

                        plan.Updates.Add(new UpdateCommand(existingByIdentity.CardId, identity, mergedOwned, mergedTrade));

                        card.CardId = existingByIdentity.CardId;
                        card.CardsOwned = mergedOwned;
                        card.CardsForTrade = mergedTrade;

                        upsertsByIdentity[identity] = card;
                    }

                    continue;
                }

                // -------- EDIT --------
                var currentId = card.CardId ?? throw new InvalidOperationException("Edit requires CardId");

                // EDIT without identity collision
                if (existingByIdentity is null || existingByIdentity.CardId == currentId)
                {
                    plan.Updates.Add(new UpdateCommand(currentId, identity, card.CardsOwned, card.CardsForTrade));

                    upsertsByIdentity[identity] = card;
                    continue;
                }

                // -------- EDIT with identity collision (merge) --------
                var survivorId = existingByIdentity.CardId;

                var mergedOwnedFinal = existingByIdentity.CardsOwned + card.CardsOwned;
                var mergedTradeFinal = existingByIdentity.CardsForTrade + card.CardsForTrade;

                plan.DeleteIds.Add(currentId);
                removedIds.Add(currentId);

                plan.Updates.Add(new UpdateCommand(survivorId, identity, mergedOwnedFinal, mergedTradeFinal));

                card.CardId = survivorId;
                card.CardsOwned = mergedOwnedFinal;
                card.CardsForTrade = mergedTradeFinal;

                upsertsByIdentity[identity] = card;
            }

            plan.ChangeSet = new CollectionChangeSet<CardSet>
            {
                RemovedIds = [.. removedIds],
                AddedOrUpdated = [.. upsertsByIdentity.Values]
            };

            return plan;
        }
        private static CollectionIdentity GetIdentity(CardSet card)
        {
            return new CollectionIdentity(
                card.Uuid ?? throw new InvalidOperationException("Uuid required"),
                card.SelectedCondition ?? throw new InvalidOperationException("Condition required"),
                card.Language ?? throw new InvalidOperationException("Language required"),
                card.SelectedFinish ?? throw new InvalidOperationException("Finish required"));
        }







        // old

        //public async Task<IReadOnlyList<CollectionChangeSet<CardSet>>> SaveBatchAsync(IEnumerable<CardSet> cards, bool isEdit, SQLiteConnection connection)
        //{
        //    var results = new List<CollectionChangeSet<CardSet>>();

        //    if (isEdit)
        //    {
        //        foreach (var card in cards)
        //        {
        //            results.Add(await PersistEditedCardsAndReturnChangesAsync(card, connection));
        //        }
        //    }
        //    else
        //    {
        //        foreach (var card in cards)
        //        {
        //            results.Add(await PersistAddedCardsAndReturnChangesAsync(card, connection));
        //        }
        //    }

        //    return results;
        //}
        //private async Task<CollectionChangeSet<CardSet>> PersistAddedCardsAndReturnChangesAsync(CardSet card, SQLiteConnection connection)
        //{
        //    using var transaction = connection.BeginTransaction();

        //    try
        //    {
        //        // Treat add as an upsert + merge
        //        var mergeResult = await UpdateOrMergeCardAsync(card, connection, isEdit: false);

        //        transaction.Commit();

        //        // Survivor must exist for add flow
        //        Debug.Assert(mergeResult.Survivor is not null, "Survivor should never be null for Add flow.");

        //        var survivor = mergeResult.Survivor!;

        //        // Sync caller-owned instance with DB survivor
        //        card.CardId = survivor.CardId;
        //        card.CardsOwned = survivor.CardsOwned;
        //        card.CardsForTrade = survivor.CardsForTrade;
        //        card.SelectedCondition = survivor.SelectedCondition;
        //        card.SelectedFinish = survivor.SelectedFinish;
        //        card.Language = survivor.Language;

        //        return new CollectionChangeSet<CardSet>
        //        {
        //            AddedOrUpdated = [card],
        //            RemovedIds = mergeResult.Removed
        //        };
        //    }
        //    catch
        //    {
        //        transaction.Rollback();
        //        throw;
        //    }
        //}
        //private async Task<CollectionChangeSet<CardSet>> PersistEditedCardsAndReturnChangesAsync(CardSet card, SQLiteConnection connection)
        //{
        //    // Deletion-by-zero
        //    if (card.CardsOwned == 0)
        //    {
        //        await _repo.DeleteCardByIdAsync(card, connection);

        //        var deletedId = card.CardId ?? throw new InvalidOperationException("Cannot delete a card without an ID");

        //        return new CollectionChangeSet<CardSet>
        //        {
        //            RemovedIds = [deletedId]
        //        };
        //    }

        //    using var transaction = connection.BeginTransaction();

        //    try
        //    {
        //        var mergeResult = await UpdateOrMergeCardAsync(card, connection, isEdit: true);

        //        transaction.Commit();

        //        // Survivor must exist for edit flow
        //        Debug.Assert(mergeResult.Survivor is not null, "Survivor should never be null for Edit flow.");

        //        var survivor = mergeResult.Survivor!;

        //        // Sync in-memory card with DB survivor
        //        card.CardId = survivor.CardId;
        //        card.CardsOwned = survivor.CardsOwned;
        //        card.CardsForTrade = survivor.CardsForTrade;
        //        card.SelectedCondition = survivor.SelectedCondition;
        //        card.SelectedFinish = survivor.SelectedFinish;
        //        card.Language = survivor.Language;

        //        return new CollectionChangeSet<CardSet>
        //        {
        //            AddedOrUpdated = [card],
        //            RemovedIds = mergeResult.Removed
        //        };
        //    }
        //    catch
        //    {
        //        transaction.Rollback();
        //        throw;
        //    }
        //}
        //private async Task<MergeResult> UpdateOrMergeCardAsync(CardSet card, SQLiteConnection conn, bool isEdit)
        //{
        //    if (isEdit && card.CardsOwned == 0)
        //    {
        //        await _repo.DeleteCardByIdAsync(card, conn);

        //        var deletedId = card.CardId ?? throw new InvalidOperationException("Cannot delete card without ID");

        //        return new MergeResult(Survivor: null, Removed: [deletedId]);
        //    }

        //    var matchIds = await _repo.FindRecordByIdAsync(card.Uuid!, card.SelectedCondition!, card.Language!, card.SelectedFinish!, conn) ?? [];

        //    // ADD without merge
        //    if (!isEdit && matchIds.Count == 0)
        //    {
        //        var newId = await _repo.AddCardAndReturnIdAsync(card, conn);
        //        card.CardId = newId;

        //        return new MergeResult(Survivor: card, Removed: []);
        //    }

        //    // EDIT: ensure current row participates
        //    if (isEdit && card.CardId.HasValue && !matchIds.Contains(card.CardId.Value))
        //    {
        //        matchIds.Add(card.CardId.Value);
        //    }

        //    matchIds.Sort();
        //    var keepId = matchIds[0];
        //    var removedIds = matchIds.Skip(1).ToList();

        //    // EDIT with no merge -> simple update
        //    if (isEdit && removedIds.Count == 0 && keepId == card.CardId)
        //    {
        //        await _repo.UpdateCardFieldsByIdAsync(keepId, card.CardsOwned, card.CardsForTrade, card.SelectedCondition!, card.Language!, card.SelectedFinish!, conn);
        //        return new MergeResult(Survivor: card, Removed: []);
        //    }

        //    // Compute totals
        //    int sumOwned;
        //    int sumTrade;

        //    if (!isEdit)
        //    {
        //        // ADD: include all existing
        //        var (existingOwned, existingTrade) = await _repo.GetTotalsAsync(card.Uuid!, card.SelectedCondition!, card.Language!, card.SelectedFinish!, conn);

        //        sumOwned = existingOwned + card.CardsOwned;
        //        sumTrade = existingTrade + card.CardsForTrade;
        //    }
        //    else
        //    {
        //        // EDIT: exclude current row
        //        var currentId = card.CardId ?? throw new InvalidOperationException("Edit requires CardId");

        //        var (ownedWithoutCurrent, tradeWithoutCurrent) =
        //            await _repo.GetTotalsExcludingIdAsync(card.Uuid!, card.SelectedCondition!, card.Language!, card.SelectedFinish!, currentId, conn);

        //        sumOwned = ownedWithoutCurrent + card.CardsOwned;
        //        sumTrade = tradeWithoutCurrent + card.CardsForTrade;
        //    }

        //    if (removedIds.Count > 0)
        //    {
        //        await _repo.DeleteCardsByIdsAsync(removedIds, conn);
        //    }

        //    await _repo.UpdateCardFieldsByIdAsync(keepId, sumOwned, sumTrade, card.SelectedCondition!, card.Language!, card.SelectedFinish!, conn);

        //    card.CardId = keepId;
        //    card.CardsOwned = sumOwned;
        //    card.CardsForTrade = sumTrade;

        //    return new MergeResult(Survivor: card, Removed: removedIds);
        //}
        //private sealed record MergeResult(CardSet? Survivor, IReadOnlyList<int> Removed);
        //public CollectionChangeSet<CardSet> CreateCollectionChangeSetFromEdits(IEnumerable<CollectionChangeSet<CardSet>> changeSets)
        //{
        //    if (changeSets is null)
        //    {
        //        return new CollectionChangeSet<CardSet>();
        //    }

        //    var removedIds = new HashSet<int>();
        //    var upsertsByKey = new Dictionary<string, CardSet>(StringComparer.OrdinalIgnoreCase);

        //    foreach (var changeSet in changeSets)
        //    {
        //        // Collect removals
        //        foreach (var id in changeSet.RemovedIds)
        //        {
        //            removedIds.Add(id);
        //        }

        //        // Collect upserts (last writer wins per business key)
        //        foreach (var card in changeSet.AddedOrUpdated)
        //        {
        //            var key = BuildKey(card.Uuid!, card.Language!, card.SelectedFinish!, card.SelectedCondition!);

        //            upsertsByKey[key] = card;

        //            // A survivor must never be removed
        //            if (card.CardId is int survivorId)
        //            {
        //                removedIds.Remove(survivorId);
        //            }
        //        }
        //    }

        //    return new CollectionChangeSet<CardSet>
        //    {
        //        RemovedIds = [.. removedIds],
        //        AddedOrUpdated = [.. upsertsByKey.Values]
        //    };
        //}
        //private static string BuildKey(string uuid, string language, string finish, string condition) => $"{uuid}|{language}|{finish}|{condition}";

    }
}
