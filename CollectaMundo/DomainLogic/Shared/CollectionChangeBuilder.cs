using CollectaMundo.DomainLogic.CardLists.Models;

namespace CollectaMundo.DomainLogic.Shared
{
    // Pure domain logic for collapsing a sequence of card changes into a deterministic set of collection mutations.
    public static class CollectionChangeBuilder
    {
        public static CollectionChangeSet<CardSet> Build(IEnumerable<CardChangeEventArgs> changes)
        {
            if (changes is null)
            {
                return new CollectionChangeSet<CardSet>();
            }

            var removedIds = new HashSet<int>();
            var upsertsByKey = new Dictionary<string, CardSet>(StringComparer.OrdinalIgnoreCase);

            foreach (var change in changes)
            {
                foreach (var id in change.Removed)
                {
                    removedIds.Add(id);
                }

                if (change.Type == CardChangeEventArgs.ChangeType.Upsert && change.Survivor is not null)
                {
                    var s = change.Survivor;
                    var key = Build(s.Uuid!, s.Language!, s.SelectedFinish!, s.SelectedCondition!);

                    upsertsByKey[key] = s;

                    if (s.CardId is int survivorId)
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
        private static string Build(string uuid, string language, string finish, string condition) => $"{uuid}|{language}|{finish}|{condition}";
    }
}

