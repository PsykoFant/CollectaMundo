using CollectaMundo.ApplicationServices.Import.Models;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.ViewModels;
using System.Diagnostics;

namespace CollectaMundo.DomainLogic.CardLists
{
    public sealed class MyCollectionChangeLogic : IMyCollectionChangeLogic
    {
        public CollectionChangeSet<CardSet> BuildChangeSet(CollectionMutation mutation, CardViewModel myCollection, CardViewModel allCards)
        {
            var stopwatch = Stopwatch.StartNew();

            var addedOrUpdated = new List<CardSet>();

            // Build snapshot from in-memory collection
            var snapshot = CollectionSnapshot.From(myCollection.Cards);

            // Build fast lookup for CardId --> CardSet
            var cardById = myCollection.Cards.Where(c => c.CardId.HasValue).ToDictionary(c => c.CardId!.Value);

            // Build fast lookup for UUID --> Core
            var coreByUuid = allCards.Cards.Select(c => c.Core!).ToDictionary(c => c.Uuid, StringComparer.OrdinalIgnoreCase);

            foreach (var row in mutation.UpsertedRows)
            {
                var identity = row.Identity;

                // CASE A: Already exists in memory --> update quantities
                if (snapshot.TryGetById(row.CardId, out var existingRow))
                {
                    cardById.TryGetValue(row.CardId, out var existingCard);
                    if (existingCard != null)
                    {
                        existingCard.CardsOwned = row.CardsOwned + existingCard.CardsOwned;
                        existingCard.CardsForTrade = row.CardsForTrade + existingCard.CardsForTrade;

                        addedOrUpdated.Add(existingCard);
                        continue;
                    }
                }

                // CASE B: New card --> hydrate from Core
                var uuid = identity.Uuid ?? throw new InvalidOperationException("Import identity must have a UUID.");

                if (!coreByUuid.TryGetValue(uuid, out var core))
                {
                    Debug.WriteLine($"[ERROR] Core not found for UUID: {uuid}");
                    throw new InvalidOperationException($"[ERROR] Core not found for UUID: {uuid}");
                }

                var card = CardSet.FromCoreWithCollection(core,
                    cardId: row.CardId,
                    cardsOwned: row.CardsOwned,
                    cardsForTrade: row.CardsForTrade,
                    condition: identity.Condition,
                    language: identity.Language,
                    finish: identity.Finish);

                addedOrUpdated.Add(card);
            }

            var changeSet = new CollectionChangeSet<CardSet>
            {
                RemovedIds = mutation.RemovedIds,
                AddedOrUpdated = addedOrUpdated
            };

            stopwatch.Stop();
            Debug.WriteLine($"[Import] OnImportCollectionMutationRequested completed in {stopwatch.Elapsed.TotalSeconds:F2} seconds.");

            return changeSet;
        }
        public void ApplyMyCollectionChanges(IList<CardSet> collection, CollectionChangeSet<CardSet> changes)
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
    }
}
