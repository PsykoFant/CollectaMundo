using CollectaMundo.Data;
using CollectaMundo.DomainLogic.Models;
using System.Diagnostics;

namespace CollectaMundo.DomainLogic
{
    public class EditCollectionLogic(IEditCollectionRepository repo) : IEditCollectionLogic
    {
        private readonly IEditCollectionRepository _repo = repo;
        public async Task AddOrUpdateCardAsync(CardSet card)
        {
            var existing = await _repo.CheckForExistingCardAsync(card);
            if (existing.HasValue)
            {
                Debug.WriteLine($"Opdaterer eksisterende kort...");
                card.CardId = existing.Value;
                await _repo.UpdateCardAsync(card);
            }
            else
            {
                Debug.WriteLine($"Fandt ikke eksisterende kort - tilføjer nyt kort");
                await _repo.AddCardAsync(card);
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
    }
}
