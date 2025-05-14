using CollectaMundo.Data;
using CollectaMundo.DomainLogic.Models;

namespace CollectaMundo.DomainLogic
{
    public class EditCollectionLogic(IEditCollectionRepository repo) : IEditCollectionLogic
    {
        private readonly IEditCollectionRepository _repo = repo;
        public async Task<CardSet> PrepareCardForListAsync(CardSet selectedCard, bool isEdit)
        {
            if (selectedCard.Uuid == null)
            {
                throw new ArgumentException("UUID cannot be null", nameof(selectedCard));
            }

            var cardId = isEdit ? selectedCard.CardId : null;
            var languages = await _repo.FetchLanguagesForCardAsync(selectedCard.Uuid);
            var finishes = await _repo.FetchFinishesForCardAsync(selectedCard.Uuid);

            var chosenFinish = isEdit ? selectedCard.SelectedFinish : finishes.FirstOrDefault();
            var chosenCondition = isEdit ? selectedCard.SelectedCondition : "Near Mint";
            var language = isEdit ? selectedCard.Language : (selectedCard.Language ?? "English");
            var ownedCount = isEdit ? selectedCard.CardsOwned : 1;
            var tradeCount = isEdit ? selectedCard.CardsForTrade : 0;

            return new CardSet
            {
                CardId = cardId,
                Name = selectedCard.Name,
                SetName = selectedCard.SetName,
                Uuid = selectedCard.Uuid,
                CardsOwned = ownedCount,
                CardsForTrade = tradeCount,
                AvailableFinishes = finishes,
                SelectedFinish = chosenFinish,
                Language = language,
                OtherLanguages = languages,
                SelectedCondition = chosenCondition,
            };
        }

        // Prepare a new card directly for submission to db with defaults (taking into account non-English or non-nonfoil cards)
        public async Task<CardSet> PrepareNewCardWithDefaultsAsync(CardSet selectedCard)
        {
            if (selectedCard.Uuid == null)
            {
                throw new ArgumentException("UUID is required", nameof(selectedCard));
            }

            // 1) grab all finishes / languages
            var finishes = await _repo.FetchFinishesForCardAsync(selectedCard.Uuid);
            var languages = await _repo.FetchLanguagesForCardAsync(selectedCard.Uuid);

            // 2) pick “nonfoil” if available, else first; same for English
            string chosenFinish = finishes
                .FirstOrDefault(f => f.Equals("nonfoil", StringComparison.OrdinalIgnoreCase))
                ?? finishes.FirstOrDefault()
                ?? "nonfoil";

            string chosenLanguage = languages
                .FirstOrDefault(l => l.Equals("English", StringComparison.OrdinalIgnoreCase))
                ?? languages.FirstOrDefault()
                ?? "English";

            // 3) build CardSet
            return new CardSet
            {
                Uuid = selectedCard.Uuid,
                Name = selectedCard.Name,
                SelectedFinish = chosenFinish,
                SelectedCondition = "Near Mint",
                Language = chosenLanguage,
                CardsOwned = 1,
                CardsForTrade = 0
            };
        }

        // Save a card and return the changes to viewmodel
        public async Task<IReadOnlyList<CardChangeEventArgs>> SaveBatchAsync(IEnumerable<CardSet> raws, bool isEdit)
        {
            var changes = new List<CardChangeEventArgs>();

            // 1) Open the connection once
            await DBAccess.OpenConnectionAsync();

            // 2) Grab the connection and null‐check it
            var conn = DBAccess.connection ?? throw new InvalidOperationException("Database connection was null after opening.");

            // 3) Begin a transaction on that guaranteed‐non‐null conn
            using var tx = conn.BeginTransaction();

            try
            {
                // 3) For each card, invoke your existing logic
                foreach (var raw in raws)
                {
                    var change = await SaveAndReturnChangesAsync(raw, isEdit);
                    changes.Add(change);
                }

                // 4) Commit if all succeeded
                tx.Commit();
            }
            catch
            {
                // 5) Roll back on any failure
                tx.Rollback();
                throw;
            }
            finally
            {
                // 6) Close connection
                DBAccess.CloseConnection();
            }

            return changes;
        }
        private async Task<CardChangeEventArgs> SaveAndReturnChangesAsync(CardSet raw, bool isEdit)
        {
            // 1) Persist (insert/update/delete-by-zero)
            await PersistAsync(raw, isEdit);

            // 2) If delete-by-zero, short-circuit
            if (isEdit && raw.CardsOwned == 0)
                return new CardChangeEventArgs(raw.CardId!.Value);

            // 3) Get all matching IDs
            var allIds = await FetchMatchingIdsAsync(raw);

            // 4) Merge duplicates *and* return our new totals
            var (keepId, sumOwned, sumTrade, removed) = await MergeDuplicatesIfNeededAsync(raw, allIds);

            // 5) Build the survivor in-memory
            raw.CardId = keepId;
            raw.CardsOwned = sumOwned;
            raw.CardsForTrade = sumTrade;

            // 6) Fire the upsert event
            return new CardChangeEventArgs(raw, removed);
        }

        // Persist the single incoming CardSet (insert / update / delete)
        private async Task PersistAsync(CardSet card, bool isEdit)
        {
            if (isEdit && card.CardsOwned == 0)
            {
                await _repo.DeleteCardByIdAsync(card);
            }
            else if (isEdit)
            {
                await _repo.UpdateCardAsync(card);
            }
            else
            {
                var existingId = await _repo.FindExistingCardReturnIdAsync(card);
                if (existingId.HasValue)
                {
                    card.CardId = existingId.Value;
                    await _repo.UpdateCardCountsAsync(card);
                }
                else
                {
                    await _repo.AddCardAsync(card);
                }
            }
        }

        // Fetch all record-IDs sharing the same business key
        private Task<List<int>> FetchMatchingIdsAsync(CardSet card) => _repo.FindRecordByIdAsync(card.Uuid!, card.SelectedCondition!, card.Language!, card.SelectedFinish!);

        // If there are duplicates, merge sums in DB and return the IDs we deleted
        private async Task<(int keepId, int sumOwned, int sumTrade, int[] removed)> MergeDuplicatesIfNeededAsync(CardSet card, List<int> allIds)
        {
            if (allIds.Count <= 1)
            {
                // no merge needed:
                var id = allIds.Count == 1 ? allIds[0] : card.CardId!.Value;
                return (id, card.CardsOwned, card.CardsForTrade, Array.Empty<int>());
            }

            allIds.Sort();
            var keepId = allIds[0];
            var removed = allIds.Skip(1).ToArray();

            // repo call returns the new sums:
            var (sumOwned, sumTrade) = await _repo.MergeDuplicateRecordsAsync(card.Uuid!, card.SelectedCondition!, card.Language!, card.SelectedFinish!, keepId);

            return (keepId, sumOwned, sumTrade, removed);
        }
    }
}
