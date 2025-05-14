using CollectaMundo.DomainLogic;
using CollectaMundo.DomainLogic.Models;
using System.Collections.ObjectModel;

namespace CollectaMundo.ApplicationServices
{
    public class EditCollectionCoordinator(IUnitOfWork uow, IEditCollectionLogic domainLogic) : IEditCollectionCoordinator
    {
        private readonly IEditCollectionLogic _domainLogic = domainLogic ?? throw new ArgumentNullException(nameof(domainLogic));
        private readonly IUnitOfWork _uow = uow ?? throw new ArgumentNullException(nameof(uow));

        // Adding cards to an add or edit listview
        public Task AddCardToAddCardsListViewAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection) => AddCardToListViewHelperAsync(selectedCard, targetCollection, false);
        public Task AddCardToEditCardsListViewAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection) => AddCardToListViewHelperAsync(selectedCard, targetCollection, true);
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

        // Submitting cards to the database
        /// <summary>
        /// Prepare defaults for a batch of new cards, then SubmitCardBatchAsync them all at once.
        /// </summary>
        public async Task<List<CardChangeEventArgs>> SubmitNewCardsWithDefaultsBatchAsync(IEnumerable<CardSet> raws)
        {
            // 1) Prepare each raw into a fully populated CardSet
            var prepared = new List<CardSet>();
            foreach (var raw in raws)
            {
                prepared.Add(await _domainLogic.PrepareNewCardWithDefaultsAsync(raw));
            }

            // 2) Save them all in one transaction/batch
            return await SubmitCardBatchAsync(prepared);
        }
        public async Task<List<CardChangeEventArgs>> SubmitCardBatchAsync(IEnumerable<CardSet> cards)
        {
            bool isEdit = cards.Any(c => c.CardId != null);

            // 1) start transaction
            await _uow.BeginAsync();

            try
            {
                // 2) hand off to pure domain logic (no DB calls here)
                var results = await _domainLogic.SaveBatchAsync(cards, isEdit);

                // 3) commit
                await _uow.CommitAsync();

                // 4) return
                return [.. results];
            }
            catch
            {
                // 5) rollback on any error
                await _uow.RollbackAsync();
                throw;
            }
            finally
            {
                // 6) tear down connection
                await _uow.DisposeAsync();
            }
        }
    }
}
