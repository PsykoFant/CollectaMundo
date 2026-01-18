using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Import.Models;
using CollectaMundo.DomainLogic.Shared;

namespace CollectaMundo.DomainLogic.Import
{
    // Builds a CollectionChangeSet<CardSet> from import results. Uses BUSINESS KEY matching only (uuid + language + finish + condition).
    public static class ImportCollectionChangeFactory
    {
        public static CollectionChangeSet<CardSet> CreateCollectionChangeSet(IReadOnlyList<CollectionUpsertItem> upserts, IReadOnlyList<CardSet> currentCollection)
        {
            if (upserts == null || upserts.Count == 0)
            {
                return new CollectionChangeSet<CardSet>();
            }

            var removedIds = new HashSet<int>();
            var addedOrUpdated = new List<CardSet>();

            foreach (var upsert in upserts)
            {
                // Find all cards in collection matching the business key
                var matches = currentCollection
                    .Where(c =>
                        string.Equals(c.Uuid, upsert.Uuid, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(c.Language, upsert.Language, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(c.SelectedFinish, upsert.Finish, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(c.SelectedCondition, upsert.Condition, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (matches.Count == 0)
                {
                    // ADD
                    var newCard = new CardSet
                    {
                        Uuid = upsert.Uuid,
                        Language = upsert.Language,
                        SelectedFinish = upsert.Finish,
                        SelectedCondition = upsert.Condition,
                        CardsOwned = upsert.CardsOwned,
                        CardsForTrade = upsert.CardsForTrade
                    };

                    newCard.RecomputeCollectionPrice();
                    addedOrUpdated.Add(newCard);
                }
                else
                {
                    // UPDATE survivor (first is arbitrary but deterministic)
                    var survivor = matches[0];

                    survivor.CardsOwned = upsert.CardsOwned;
                    survivor.CardsForTrade = upsert.CardsForTrade;
                    survivor.RecomputeCollectionPrice();

                    addedOrUpdated.Add(survivor);

                    // Remove duplicates
                    foreach (var dup in matches.Skip(1))
                    {
                        if (dup.CardId.HasValue)
                        {
                            removedIds.Add(dup.CardId.Value);
                        }
                    }
                }
            }

            return new CollectionChangeSet<CardSet>
            {
                RemovedIds = [.. removedIds],
                AddedOrUpdated = addedOrUpdated
            };
        }
    }
}
