using CollectaMundo.DomainLogic.CardLists.Models;
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
    }
}
