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

        // Adds a new card or updates an existing one.
        public async Task AddOrUpdateCardAsync(CardSet card, ObservableCollection<CardSet> inMemoryCollection)
        {
            try
            {
                // Ensure the database is ready (DBAccess is leveraged within the data service)
                int? existingId = await _dataService.CheckForExistingCardAsync(card);

                if (existingId.HasValue)
                {
                    // Card exists in DB. Update card details.
                    card.CardId = existingId.Value;
                    await _dataService.UpdateCardAsync(card);

                    // Find the corresponding card in the in-memory collection and update it.
                    var existingCard = inMemoryCollection.FirstOrDefault(c =>
                        c.Uuid == card.Uuid &&
                        c.SelectedCondition == card.SelectedCondition &&
                        c.Language == card.Language &&
                        c.SelectedFinish == card.SelectedFinish);

                    if (existingCard != null)
                    {
                        existingCard.CardsOwned += card.CardsOwned;
                        existingCard.CardsForTrade += card.CardsForTrade;
                    }
                }
                else
                {
                    // Card does not exist. Insert new record in DB.
                    await _dataService.AddCardAsync(card);
                    // Add the card to the in-memory collection.
                    inMemoryCollection.Add(card);
                }
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
