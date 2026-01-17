using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Shared;
using System.Collections.ObjectModel;

namespace CollectaMundo.ViewModels.Shared
{
    public sealed class CollectionChangeApplier : ICollectionChangeApplier<CardSet>
    {
        public void Apply(ObservableCollection<CardSet> collection, CollectionChangeSet<CardSet> changes)
        {
            if (collection == null || changes == null)
            {
                return;
            }

            // -----------------------------
            // Remove deleted / merged cards
            // -----------------------------
            if (changes.RemovedIds.Count > 0)
            {
                var toRemove = collection
                    .Where(c => c.CardId.HasValue && changes.RemovedIds.Contains(c.CardId.Value)).ToList();

                foreach (var card in toRemove)
                {
                    collection.Remove(card);
                }
            }

            // -----------------------------
            // Add or update survivors
            // -----------------------------
            foreach (var incoming in changes.AddedOrUpdated)
            {
                if (incoming.CardId is int cardId)
                {
                    var existing = collection.FirstOrDefault(c => c.CardId == cardId);

                    if (existing != null)
                    {
                        ReplaceCard(existing, incoming, collection);
                        continue;
                    }
                }

                collection.Add(incoming);
            }
        }

        private static void ReplaceCard(CardSet oldCard, CardSet newCard, ObservableCollection<CardSet> collection)
        {
            var index = collection.IndexOf(oldCard);
            if (index >= 0)
            {
                collection[index] = newCard;
            }
        }
    }
}
