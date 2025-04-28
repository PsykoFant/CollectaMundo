using CollectaMundo.Data;        // ICardRepository
using CollectaMundo.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace CollectaMundo.UICoordinators
{
    public class CardCollectionCoordinator(IEditCollectionRepository dataService) : ICardCollectionCoordinator
    {
        private readonly IEditCollectionRepository _repository = dataService ?? throw new ArgumentNullException(nameof(dataService));

        // Public wrappers
        public Task AddCardToAddCardsListViewAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection) => AddCardToListViewAsync(selectedCard, targetCollection, isEdit: false);

        public Task AddCardToEditCardsListViewAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection) => AddCardToListViewAsync(selectedCard, targetCollection, isEdit: true);

        // 3) Common implementation
        private async Task AddCardToListViewAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection, bool isEdit)
        {
            if (selectedCard.Uuid == null)
                throw new InvalidOperationException("Card UUID is null, cannot fetch metadata.");

            // 1) Fetch languages & finishes
            await DBAccess.OpenConnectionAsync();
            var languages = await _repository.FetchLanguagesForCardAsync(selectedCard.Uuid);
            var finishes = await _repository.FetchFinishesForCardAsync(selectedCard.Uuid);
            DBAccess.CloseConnection();

            // 2) Decide parameters based on mode
            string selectedFinish = isEdit ? selectedCard.SelectedFinish : finishes.FirstOrDefault();
            string selectedCondition = isEdit ? selectedCard.SelectedCondition : "Near Mint";
            string language = isEdit ? selectedCard.Language : (selectedCard.Language ?? "English");
            int cardsOwned = isEdit ? selectedCard.CardsOwned : 1;
            int cardsForTrade = isEdit ? selectedCard.CardsOwned : 0;

            // 3) Skip if already present
            bool exists = targetCollection.Any(c =>
                c.Uuid == selectedCard.Uuid &&
                c.SelectedFinish == selectedFinish &&
                c.SelectedCondition == selectedCondition &&
                c.Language == language);
            if (exists) return;

            // 4) Create & add
            var newItem = new CardSet
            {
                Name = selectedCard.Name,
                SetName = selectedCard.SetName,
                Uuid = selectedCard.Uuid,
                CardsOwned = cardsOwned,
                CardsForTrade = cardsForTrade,
                AvailableFinishes = finishes,
                SelectedFinish = selectedFinish,
                Language = language,
                OtherLanguages = languages,
                SelectedCondition = selectedCondition,
            };
            targetCollection.Add(newItem);
        }

        // 4) Database operations delegated to repository
        public async Task AddOrUpdateCardAsync(CardSet card)
        {
            try
            {
                await DBAccess.OpenConnectionAsync();
                int? existingId = await _repository.CheckForExistingCardAsync(card);

                if (existingId.HasValue)
                {
                    card.CardId = existingId.Value;
                    await _repository.UpdateCardAsync(card);
                }
                else
                {
                    await _repository.AddCardAsync(card);
                }

                DBAccess.CloseConnection();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in AddOrUpdateCardAsync: {ex.Message}");
                throw;
            }
        }

        // 5) In-memory synchronization
        public async Task UpdateCardDetailsAsync(CardSet card, ObservableCollection<CardSet> inMemoryCollection)
        {
            try
            {
                await _repository.UpdateCardAsync(card);
                var existingCard = inMemoryCollection.FirstOrDefault(c => c.CardId == card.CardId);
                if (existingCard != null)
                {
                    existingCard.CardsOwned = card.CardsOwned;
                    existingCard.CardsForTrade = card.CardsForTrade;
                    existingCard.SelectedCondition = card.SelectedCondition;
                    existingCard.Language = card.Language;
                    existingCard.SelectedFinish = card.SelectedFinish;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in UpdateCardDetailsAsync: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteCardAsync(CardSet card, ObservableCollection<CardSet> inMemoryCollection)
        {
            try
            {
                await _repository.DeleteCardAsync(card);
                inMemoryCollection.Remove(card);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in DeleteCardAsync: {ex.Message}");
                throw;
            }
        }
    }
}
