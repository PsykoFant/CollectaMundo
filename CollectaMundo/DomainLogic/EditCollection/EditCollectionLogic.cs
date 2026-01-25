using CollectaMundo.ApplicationServices.EditCollection.Models;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.EditCollection.Models;
using CollectaMundo.DomainLogic.Import.Models;
using CollectaMundo.DomainLogic.Shared;
using System.Diagnostics;

namespace CollectaMundo.DomainLogic.EditCollection
{
    public class EditCollectionLogic() : IEditCollectionLogic
    {
        public CardSet PrepareCardForList(CardSet selectedCard, CardToAddMetadataDto metadata, bool isEdit)
        {
            if (selectedCard.Core is null)
            {
                throw new InvalidOperationException("CardSet.Core must be set. Use CardSet.FromCore.");
            }

            var clone = CardSet.FromCore(selectedCard.Core);

            // carry over view-only fields
            clone.SelectedFinish = selectedCard.SelectedFinish;
            clone.SelectedCondition = selectedCard.SelectedCondition;
            clone.Count = selectedCard.Count;

            // attach metadata
            clone.AvailableFinishes = [.. metadata.AvailableFinishes];
            clone.OtherLanguages = NormalizeLanguages(metadata.AvailableLanguages, selectedCard.Language);

            if (isEdit)
            {
                clone.CardId = selectedCard.CardId;
                clone.CardsOwned = selectedCard.CardsOwned;
                clone.CardsForTrade = selectedCard.CardsForTrade;
                clone.Language = selectedCard.Language!;
                clone.SelectedFinish = selectedCard.SelectedFinish!;
                clone.SelectedCondition = selectedCard.SelectedCondition!;
            }
            else
            {
                ApplyNewDefaults(clone);
            }

            clone.RecomputeCollectionPrice();
            return clone;
        }
        public CardSet PrepareNewCardWithDefaults(CardSet selectedCard, CardToAddMetadataDto metadata)
        {
            if (selectedCard.Core is null)
            {
                throw new InvalidOperationException(
                    "CardSet.Core must be set. Use CardSet.FromCore.");
            }

            var clone = CardSet.FromCore(selectedCard.Core);

            // Attach metadata (copy, do not share)
            clone.AvailableFinishes = metadata.AvailableFinishes.ToList();
            clone.OtherLanguages = NormalizeLanguages(
                metadata.AvailableLanguages,
                selectedCard.Language);

            // Apply defaults for new cards
            ApplyNewDefaults(clone);

            clone.RecomputeCollectionPrice();
            return clone;
        }
        public EditBatchPlan PlanBatch(IEnumerable<CardSet> cards, ICollectionSnapshot snapshot, bool isEdit)
        {
            Debug.WriteLine($"[PlanBatch] START isEdit={isEdit}");

            var plan = new EditBatchPlan();
            var removedIds = new HashSet<int>();
            var upsertsByIdentity = new Dictionary<CollectionIdentity, CardSet>();

            foreach (var card in cards)
            {
                // -------- EDIT: deletion-by-zero --------
                if (isEdit && card.CardsOwned == 0)
                {
                    var id = card.CardId ?? throw new InvalidOperationException("Edit requires CardId");
                    plan.DeleteIds.Add(id);
                    removedIds.Add(id);
                    continue;
                }

                var identity = CollectionIdentityFactory.Create(card.Uuid, card.SelectedCondition, card.Language, card.SelectedFinish);

                snapshot.TryGetByIdentity(identity, out var existingByIdentity);

                if (!isEdit)
                {
                    // ADD flow
                    if (existingByIdentity is null)
                    {
                        plan.Inserts.Add(new InsertCommand(identity, card.CardsOwned, card.CardsForTrade, card));
                        upsertsByIdentity[identity] = card;
                    }
                    else
                    {
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

                // EDIT flow
                var currentId = card.CardId ?? throw new InvalidOperationException("Edit requires CardId");

                if (!snapshot.TryGetById(currentId, out var originalRow))
                {
                    throw new InvalidOperationException($"CardId {currentId} not found in snapshot");
                }

                var originalIdentity = originalRow.Identity;

                if (identity.Equals(originalIdentity))
                {
                    plan.Updates.Add(new UpdateCommand(currentId, identity, card.CardsOwned, card.CardsForTrade));
                    upsertsByIdentity[identity] = card;
                    continue;
                }

                if (existingByIdentity is not null)
                {
                    var survivorId = existingByIdentity.CardId;
                    var mergedOwned = existingByIdentity.CardsOwned + card.CardsOwned;
                    var mergedTrade = existingByIdentity.CardsForTrade + card.CardsForTrade;

                    plan.DeleteIds.Add(currentId);
                    removedIds.Add(currentId);

                    plan.Updates.Add(new UpdateCommand(survivorId, identity, mergedOwned, mergedTrade));

                    card.CardId = survivorId;
                    card.CardsOwned = mergedOwned;
                    card.CardsForTrade = mergedTrade;

                    upsertsByIdentity[identity] = card;
                    continue;
                }

                plan.Updates.Add(new UpdateCommand(currentId, identity, card.CardsOwned, card.CardsForTrade));
                upsertsByIdentity[identity] = card;
            }

            plan.ChangeSet = new CollectionChangeSet<CardSet>
            {
                RemovedIds = [.. removedIds],
                AddedOrUpdated = [.. upsertsByIdentity.Values]
            };
            Debug.WriteLine($"[PlanBatch] END Deletes={plan.DeleteIds.Count} Updates={plan.Updates.Count} Inserts={plan.Inserts.Count}");
            return plan;
        }

        // Private helpers

        private static readonly string _defaultLanguage = CollectionCardItemDefaults.GetDefaultString(ImportField.Language);
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
        private static string? ChooseDefaultFinish(IReadOnlyList<string>? finishes)
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
        private static string ChooseDefaultLanguage(IReadOnlyList<string>? langs)
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
    }
}
