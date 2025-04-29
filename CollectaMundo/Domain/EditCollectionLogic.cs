using CollectaMundo.Data;
using CollectaMundo.Domain.CollectaMundo.Domain;
using CollectaMundo.Models;

namespace CollectaMundo.Domain
{
    public class EditCollectionLogic(IEditCollectionRepository repo) : IEditCollectionLogic
    {
        private readonly IEditCollectionRepository _repo = repo;
        public async Task AddOrUpdateCardAsync(CardSet card)
        {
            var existing = await _repo.CheckForExistingCardAsync(card);
            if (existing.HasValue)
                await _repo.UpdateCardAsync(card);
            else
                await _repo.AddCardAsync(card);
        }
        public async Task<CardSet> PrepareCardForListAsync(CardSet selectedCard, bool isEdit)
        {
            if (selectedCard.Uuid == null)
                throw new ArgumentException("UUID cannot be null", nameof(selectedCard));
            await DBAccess.OpenConnectionAsync();
            var languages = await _repo.FetchLanguagesForCardAsync(selectedCard.Uuid);
            var finishes = await _repo.FetchFinishesForCardAsync(selectedCard.Uuid);
            DBAccess.CloseConnection();

            var chosenFinish = isEdit ? selectedCard.SelectedFinish : finishes.FirstOrDefault();
            var chosenCondition = isEdit ? selectedCard.SelectedCondition : "Near Mint";
            var language = isEdit ? selectedCard.Language : (selectedCard.Language ?? "English");
            var ownedCount = isEdit ? selectedCard.CardsOwned : 1;
            var tradeCount = isEdit ? selectedCard.CardsForTrade : 0;

            return new CardSet
            {
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
