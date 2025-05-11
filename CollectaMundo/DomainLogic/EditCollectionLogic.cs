using CollectaMundo.Data;
using CollectaMundo.DomainLogic.Models;
using CollectaMundo.ViewModels;
using System.Diagnostics;

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
        public async Task<CardChangeEventArgs> SaveAndReturnChangesAsync(CardSet input, bool isEdit)
        {
            // 1) Insert, update, or delete the single row
            await AddOrUpdateCardAsync(input, isEdit);

            // 2) If they zero’d it out, we only need to delete that one ID
            if (isEdit && input.CardsOwned == 0)
            {
                return new CardChangeEventArgs(input.CardId!.Value);
            }

            // 3) Find all IDs with matching properties
            var allIds = await _repo.GetMatchingRecordIdsAsync(
                input.Uuid!, input.SelectedCondition!, input.Language!, input.SelectedFinish!);

            // 4) If there’s more than one, choose one to keep and merge
            int[] removed = [];
            if (allIds.Count > 1)
            {
                // keep the smallest ID
                allIds.Sort();
                var keepId = allIds[0];
                removed = [.. allIds.Skip(1)];

                // collapse sums into keepId, delete the rest
                await _repo.MergeDuplicateRecordsAsync(
                    input.Uuid!, input.SelectedCondition!, input.Language!, input.SelectedFinish!, keepId);
            }

            // 5) Re-fetch the one true survivor
            var survivor = await _repo.GetMyCollectionRecordAsync(
                input.Uuid!, input.SelectedCondition!, input.Language!, input.SelectedFinish!);

            // 6) Tell the UI both who survived and whose IDs to purge
            return new CardChangeEventArgs(survivor, removed);
        }
        private async Task AddOrUpdateCardAsync(CardSet card, bool isEdit)
        {
            // If CardsOwned is zero, delete card
            if (isEdit && card.CardsOwned == 0)
            {
                Debug.WriteLine($"Nul kort tilbage - sletter kort...");
                await _repo.DeleteCardAsync(card);
            }

            else if (isEdit)
            {
                Debug.WriteLine($"Vi redigerer kort med id: {card.CardId}...");
                await _repo.EditCardAsync(card);
            }
            else
            {
                var existing = await _repo.CheckForExistingCardAsync(card);
                if (existing.HasValue)
                {
                    Debug.WriteLine($"Opdaterer eksisterende kort...");
                    card.CardId = existing.Value;
                    await _repo.UpdateCardCountsAsync(card);
                }
                else
                {
                    Debug.WriteLine($"Fandt ikke eksisterende kort - tilføjer nyt kort");
                    await _repo.AddCardAsync(card);
                }
            }
        }
    }
}
