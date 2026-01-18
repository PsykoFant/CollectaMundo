using CollectaMundo.DomainLogic.CardLists.Models;

namespace CollectaMundo.DomainLogic.Shared
{
    // Pure domain logic for collapsing a sequence of card changes into a deterministic set of collection mutations.
    public static class CollectionChangeBuilder
    {
        public static CollectionChangeSet<CardSet> CreateCollectionChangeSetFromEdits(IEnumerable<CollectionChangeSet<CardSet>> changeSets)
        {
            if (changeSets is null)
            {
                return new CollectionChangeSet<CardSet>();
            }

            var removedIds = new HashSet<int>();
            var upsertsByKey = new Dictionary<string, CardSet>(StringComparer.OrdinalIgnoreCase);

            foreach (var changeSet in changeSets)
            {
                // Collect removals
                foreach (var id in changeSet.RemovedIds)
                {
                    removedIds.Add(id);
                }

                // Collect upserts (last writer wins per business key)
                foreach (var card in changeSet.AddedOrUpdated)
                {
                    var key = BuildKey(card.Uuid!, card.Language!, card.SelectedFinish!, card.SelectedCondition!);

                    upsertsByKey[key] = card;

                    // A survivor must never be removed
                    if (card.CardId is int survivorId)
                    {
                        removedIds.Remove(survivorId);
                    }
                }
            }

            return new CollectionChangeSet<CardSet>
            {
                RemovedIds = [.. removedIds],
                AddedOrUpdated = [.. upsertsByKey.Values]
            };
        }
        private static string BuildKey(string uuid, string language, string finish, string condition) => $"{uuid}|{language}|{finish}|{condition}";
    }

}

