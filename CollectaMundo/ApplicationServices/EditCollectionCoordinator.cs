using CollectaMundo.Data;
using CollectaMundo.DomainLogic;
using CollectaMundo.DomainLogic.Models;
using CollectaMundo.ViewModels;
using System.Collections.ObjectModel;

namespace CollectaMundo.ApplicationServices
{
    public class EditCollectionCoordinator(IEditCollectionLogic domainLogic, IEditCollectionRepository repo) : IEditCollectionCoordinator
    {
        private readonly IEditCollectionLogic _domainLogic = domainLogic ?? throw new ArgumentNullException(nameof(domainLogic));
        private readonly IEditCollectionRepository _repo = repo ?? throw new ArgumentNullException(nameof(repo));

        // Adding cards to listview
        public Task AddCardToAddCardsListViewAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection) => AddCardToListViewHelperAsync(selectedCard, targetCollection, false);
        public Task AddCardToEditCardsListViewAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection) => AddCardToListViewHelperAsync(selectedCard, targetCollection, true);
        // Common helper
        private async Task AddCardToListViewHelperAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection, bool isEdit)
        {
            // 1) Let your domain‐logic prepare the fully populated CardSet
            var newItem = await _domainLogic.PrepareCardForListAsync(selectedCard, isEdit);

            // 2) If any item in the collection already has the same database ID, skip.
            if (newItem.CardId != null && targetCollection.Any(c => c.CardId == newItem.CardId))
            {
                return;
            }

            // 3) Otherwise, fall back to matching on the other unique properties
            bool existsByKey = targetCollection.Any(c =>
                c.Uuid == newItem.Uuid &&
                c.SelectedFinish == newItem.SelectedFinish &&
                c.SelectedCondition == newItem.SelectedCondition &&
                c.Language == newItem.Language);

            if (existsByKey)
            {
                return;
            }

            // 4) If it really is new, add it to the in‐memory list
            targetCollection.Add(newItem);
        }

        // Submitting cards to database
        public async Task<CardChangeEventArgs> SubmitCollectionUpdatesAsync(CardSet card, bool isEdit)
        {
            return await _domainLogic.SaveAndReturnChangesAsync(card, isEdit);
        }
        public async Task<CardChangeEventArgs> SubmitNewCardsWithDefaultsAsync(CardSet raw)
        {
            // 1) prepare the new card (this returns Task<CardSet>)
            var toSave = await _domainLogic.PrepareNewCardWithDefaultsAsync(raw);

            // 2) now pass the real CardSet into your SaveAndReturnChangesAsync
            return await _domainLogic.SaveAndReturnChangesAsync(toSave, isEdit: false);
        }
    }
}
