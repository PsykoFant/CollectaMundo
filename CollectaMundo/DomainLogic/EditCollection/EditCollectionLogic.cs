using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.EditCollection.Models;
using CollectaMundo.DomainLogic.Import.Models;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.Infrastructure.EditCollection;
using System.Data.SQLite;

namespace CollectaMundo.DomainLogic.EditCollection
{
    public class EditCollectionLogic(IEditCollectionRepo repo) : IEditCollectionLogic
    {
        private readonly IEditCollectionRepo _repo = repo;
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
        public async Task<IReadOnlyList<CardChangeEventArgs>> SaveBatchAsync(IEnumerable<CardSet> cards, bool isEdit, SQLiteConnection connection)
        {
            // Using a single SQLiteConnection: do NOT parallelize DB ops.
            var results = new List<CardChangeEventArgs>();

            if (isEdit)
            {
                foreach (var card in cards)
                {
                    results.Add(await PersistEditedCardsAndReturnChangesAsync(card, connection));
                }
            }
            else
            {
                foreach (var card in cards)
                {
                    results.Add(await PersistAddedCardsAndReturnChangesAsync(card, connection));
                }
            }

            return results;
        }
        private async Task<CardChangeEventArgs> PersistAddedCardsAndReturnChangesAsync(CardSet card, SQLiteConnection connection)
        {
            using var transaction = connection.BeginTransaction();

            try
            {
                // Treat add as an upsert + merge
                var mergeResult = await _repo.UpdateOrMergeAsync(card, connection, transaction);

                transaction.Commit();

                // Sync in-memory card to DB survivor
                card.CardId = mergeResult.Survivor.CardId;
                card.CardsOwned = mergeResult.Survivor.CardsOwned;
                card.CardsForTrade = mergeResult.Survivor.CardsForTrade;
                card.SelectedCondition = mergeResult.Survivor.SelectedCondition;
                card.SelectedFinish = mergeResult.Survivor.SelectedFinish;
                card.Language = mergeResult.Survivor.Language;

                return new CardChangeEventArgs(card, mergeResult.Removed);
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private async Task<CardChangeEventArgs> PersistEditedCardsAndReturnChangesAsync(CardSet card, SQLiteConnection connection)
        {
            // Deletion-by-zero still valid
            if (card.CardsOwned == 0)
            {
                await _repo.DeleteCardByIdAsync(card, connection);

                var deletedId = card.CardId
                    ?? throw new InvalidOperationException("Cannot delete a card without an ID");

                return new CardChangeEventArgs(deletedId);
            }

            using var transaction = connection.BeginTransaction();

            try
            {
                var mergeResult = await _repo.UpdateOrMergeAsync(card, connection, transaction);

                transaction.Commit();

                // Update in-memory card to reflect DB survivor
                card.CardId = mergeResult.Survivor.CardId;
                card.CardsOwned = mergeResult.Survivor.CardsOwned;
                card.CardsForTrade = mergeResult.Survivor.CardsForTrade;
                card.SelectedCondition = mergeResult.Survivor.SelectedCondition;
                card.SelectedFinish = mergeResult.Survivor.SelectedFinish;
                card.Language = mergeResult.Survivor.Language;

                return new CardChangeEventArgs(card, mergeResult.Removed);
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }


    }
}
