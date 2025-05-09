using CollectaMundo.Data;
using CollectaMundo.DomainLogic.Models;
using System.Diagnostics;

namespace CollectaMundo.DomainLogic
{
    public class EditCollectionLogic(IEditCollectionRepository repo) : IEditCollectionLogic
    {
        private readonly IEditCollectionRepository _repo = repo;
        public async Task AddOrUpdateCardAsync(CardSet card, bool isEdit)
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

        /// <summary>
        /// Given a “raw” selectedCard (with only Uuid, Name populated), fetch 
        /// languages + finishes from db, pick sensible defaults (nonfoil if present,
        /// else first finish; English if present, else first language; NM condition),
        /// and set count=1, trade=0.
        /// </summary>
        public async Task<CardSet> PrepareNewCardWithDefaultsAsync(CardSet selectedCard)
        {
            if (selectedCard.Uuid == null)
            {
                throw new ArgumentException("UUID is required", nameof(selectedCard));
            }

            // 1) grab all finishes / languages
            await DBAccess.OpenConnectionAsync();
            var finishes = await _repo.FetchFinishesForCardAsync(selectedCard.Uuid);
            var languages = await _repo.FetchLanguagesForCardAsync(selectedCard.Uuid);
            DBAccess.CloseConnection();

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
    }
}
