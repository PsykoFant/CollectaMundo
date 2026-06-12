using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Shared.Models;

namespace CollectaMundo.ApplicationServices.CollectionMutations
{
    public static class CollectionChangeSetApplier
    {
        public static void Apply(IList<CollectionCard> collection, CollectionChangeSet<CollectionCard> changes)
        {
            if (collection is null || changes is null)
            {
                return;
            }

            if (changes.RemovedIds.Count > 0)
            {
                var removedIds = changes.RemovedIds.ToHashSet();

                for (int i = collection.Count - 1; i >= 0; i--)
                {
                    if (removedIds.Contains(collection[i].CardId))
                    {
                        collection.RemoveAt(i);
                    }
                }
            }

            foreach (var incoming in changes.AddedOrUpdated)
            {
                var index = -1;

                for (int i = 0; i < collection.Count; i++)
                {
                    if (collection[i].CardId == incoming.CardId)
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

                collection.Add(incoming);
            }
        }
    }
}
