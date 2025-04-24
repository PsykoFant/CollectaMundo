using CollectaMundo.Models;
using CollectaMundo.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace CollectaMundo.Managers
{
    // Domain service responsible for coordinating collection operations.
    public class CardCollectionManager(ICardCollectionService dataService)
    {
        private readonly ICardCollectionService _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));

        // Public wrappers
        public Task AddCardToAddCardsListViewAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection) => AddCardToListViewAsync(selectedCard, targetCollection, isEdit: false);

        public Task AddCardToEditCardsListViewAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection) => AddCardToListViewAsync(selectedCard, targetCollection, isEdit: true);

        // Common implementation
        private async Task AddCardToListViewAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection, bool isEdit)
        {
            if (selectedCard.Uuid == null)
                throw new InvalidOperationException("Card UUID is null, cannot fetch metadata.");

            // 1) Fetch languages & finishes
            await DBAccess.OpenConnectionAsync();
            var languages = await _dataService.FetchLanguagesForCardAsync(selectedCard.Uuid);
            var finishes = await _dataService.FetchFinishesForCardAsync(selectedCard.Uuid);
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

        // Adds a new card or updates an existing one.
        public async Task AddOrUpdateCardAsync(CardSet card)
        {
            try
            {
                await DBAccess.OpenConnectionAsync();

                // Ensure the database is ready (DBAccess is leveraged within the data service)
                int? existingId = await _dataService.CheckForExistingCardAsync(card);

                if (existingId.HasValue)
                {
                    // Card exists in DB. Update card details.
                    card.CardId = existingId.Value;

                    await _dataService.UpdateCardAsync(card);
                }
                else
                {
                    // Card does not exist. Insert new record in DB.
                    await _dataService.AddCardAsync(card);
                }

                DBAccess.CloseConnection();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in AddOrUpdateCardAsync: {ex.Message}");
                throw;
            }
        }

        // Updates the details of an existing card.
        public async Task UpdateCardDetailsAsync(CardSet card, ObservableCollection<CardSet> inMemoryCollection)
        {
            try
            {
                await _dataService.UpdateCardAsync(card);
                // Update the card in the in-memory collection.
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

        // Deletes a card from the collection.
        public async Task DeleteCardAsync(CardSet card, ObservableCollection<CardSet> inMemoryCollection)
        {
            try
            {
                await _dataService.DeleteCardAsync(card);
                // Remove the card from the in-memory collection.
                inMemoryCollection.Remove(card);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in DeleteCardAsync: {ex.Message}");
                throw;
            }
        }

        // Additional business operations (e.g., setting cards for trade) can be added here.
    }
}
