using CollectaMundo.Domain.CollectaMundo.Domain;
using CollectaMundo.Models;
using System.Collections.ObjectModel;

namespace CollectaMundo.UICoordinators
{
    public class EditCollectionCoordinator(IEditCollectionLogic domain) : ICardCollectionCoordinator
    {
        private readonly IEditCollectionLogic _domain = domain ?? throw new ArgumentNullException(nameof(domain));

        // Public wrappers
        public Task AddCardToAddCardsListViewAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection) => AddCardToListViewAsync(selectedCard, targetCollection, false);
        public Task AddCardToEditCardsListViewAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection) => AddCardToListViewAsync(selectedCard, targetCollection, true);

        // 3) Common implementation
        private async Task AddCardToListViewAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection, bool isEdit)
        {
            // Delegate “prep” logic to your domain service
            var newItem = await _domain.PrepareCardForListAsync(selectedCard, isEdit);

            // Skip if already present
            bool exists = targetCollection.Any(c =>
                c.Uuid == newItem.Uuid &&
                c.SelectedFinish == newItem.SelectedFinish &&
                c.SelectedCondition == newItem.SelectedCondition &&
                c.Language == newItem.Language);

            if (exists)
                return;

            targetCollection.Add(newItem);
        }


        // 4) Database operations delegated to repository
        public async Task AddOrUpdateCardAsync(CardSet card)
        {
            /*
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
            */
        }

        // 5) In-memory synchronization
        public async Task UpdateCardDetailsAsync(CardSet card, ObservableCollection<CardSet> inMemoryCollection)
        {
            /*
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
            */
        }
        public async Task DeleteCardAsync(CardSet card, ObservableCollection<CardSet> inMemoryCollection)
        {
            /*
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
            */
        }
    }
}
