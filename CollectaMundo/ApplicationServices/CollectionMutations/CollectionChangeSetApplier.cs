using CollectaMundo.ApplicationServices.CollectionMaterialization;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.CollectionMutations;
using CollectaMundo.DomainLogic.Shared.Models;

namespace CollectaMundo.ApplicationServices.CollectionMutations
{
    public sealed class CollectionChangeSetApplier(ICollectionMaterializer materializer) : ICollectionChangeSetApplier
    {
        private readonly ICollectionMaterializer _materializer = materializer;
        public void Apply(IList<CardSet> collection, CollectionChangeSet<CardSet> changes)
        {
            if (collection is null || changes is null)
            {
                return;
            }

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
                        collection[index] = _materializer.MergeIntoExisting(collection[index], incoming);
                        continue;
                    }
                }

                collection.Add(incoming);
            }
        }
    }
}
