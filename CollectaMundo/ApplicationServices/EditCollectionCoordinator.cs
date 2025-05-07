using CollectaMundo.Data;
using CollectaMundo.DomainLogic;
using CollectaMundo.DomainLogic.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace CollectaMundo.ApplicationServices
{
    public class EditCollectionCoordinator(IEditCollectionLogic domainLogic, IEditCollectionRepository repo) : IEditCollectionCoordinator
    {
        private readonly IEditCollectionLogic _domainLogic = domainLogic ?? throw new ArgumentNullException(nameof(domainLogic));
        private readonly IEditCollectionRepository _repo = repo ?? throw new ArgumentNullException(nameof(repo));

        // Public wrappers
        public Task AddCardToAddCardsListViewAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection) => AddCardToListViewAsync(selectedCard, targetCollection, false);
        public Task AddCardToEditCardsListViewAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection) => AddCardToListViewAsync(selectedCard, targetCollection, true);
        // Common implementation
        private async Task AddCardToListViewAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection, bool isEdit)
        {
            // 1) Let your domain‐logic prepare the fully populated CardSet
            var newItem = await _domainLogic.PrepareCardForListAsync(selectedCard, isEdit);

            Debug.WriteLine($"Nyt kort id: {newItem.CardId}");

            // 2) If any item in the collection already has the same database ID, skip.
            if (newItem.CardId != null && targetCollection.Any(c => c.CardId == newItem.CardId))
                return;

            // 3) Otherwise, fall back to matching on the other unique properties
            bool existsByKey = targetCollection.Any(c =>
                c.Uuid == newItem.Uuid &&
                c.SelectedFinish == newItem.SelectedFinish &&
                c.SelectedCondition == newItem.SelectedCondition &&
                c.Language == newItem.Language);

            if (existsByKey)
                return;

            // 4) If it really is new, add it to the in‐memory list
            targetCollection.Add(newItem);
        }

        public async Task<CardSet> AddOrUpdateAndFetchCardAsync(CardSet card)
        {
            // 1) persist changes
            await _domainLogic.AddOrUpdateCardAsync(card);

            // 2) make sure our “key” fields are set
            if (card.Uuid is null ||
                card.SelectedCondition is null ||
                card.Language is null ||
                card.SelectedFinish is null)
            {
                throw new InvalidOperationException(
                    "Cannot fetch persisted card because one or more key fields are null: " +
                    $"Uuid={card.Uuid}, Condition={card.SelectedCondition}, " +
                    $"Language={card.Language}, Finish={card.SelectedFinish}"
                );
            }

            // 3) fetch the fully-populated row
            return await _repo.GetMyCollectionRecordAsync(
                card.Uuid,
                card.SelectedCondition,
                card.Language,
                card.SelectedFinish
            );
        }


        //public async Task UpdateCardDetailsAsync(CardSet card, ObservableCollection<CardSet> inMemoryCollection)
        //{

        //    try
        //    {
        //        await _repository.UpdateCardAsync(card);
        //        var existingCard = inMemoryCollection.FirstOrDefault(c => c.CardId == card.CardId);
        //        if (existingCard != null)
        //        {
        //            existingCard.CardsOwned = card.CardsOwned;
        //            existingCard.CardsForTrade = card.CardsForTrade;
        //            existingCard.SelectedCondition = card.SelectedCondition;
        //            existingCard.Language = card.Language;
        //            existingCard.SelectedFinish = card.SelectedFinish;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.WriteLine($"Error in UpdateCardDetailsAsync: {ex.Message}");
        //        throw;
        //    }

        //}
        //public async Task DeleteCardAsync(CardSet card, ObservableCollection<CardSet> inMemoryCollection)
        //{
        //    try
        //    {
        //        await _repository.DeleteCardAsync(card);
        //        inMemoryCollection.Remove(card);
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.WriteLine($"Error in DeleteCardAsync: {ex.Message}");
        //        throw;
        //    }
        //}
    }
}
