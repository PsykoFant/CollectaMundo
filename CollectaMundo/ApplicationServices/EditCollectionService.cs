using CollectaMundo.DomainLogic.Models;
using System.Collections.ObjectModel;

namespace CollectaMundo.ApplicationServices
{
    public class EditCollectionService(IUnitOfWork uow, IEditLogicFactory logicFactory) : IEditCollectionService
    {
        private readonly IUnitOfWork _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        private readonly IEditLogicFactory _logicFactory = logicFactory;

        // Adding cards to an add or edit listview
        public Task AddCardToAddCardsListViewAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection) => AddCardToListViewHelperAsync(selectedCard, targetCollection, false);
        public Task AddCardToEditCardsListViewAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection) => AddCardToListViewHelperAsync(selectedCard, targetCollection, true);
        private async Task AddCardToListViewHelperAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection, bool isEdit)
        {
            CardSet newItem;

            // Start a UoW 
            await _uow.BeginAsync();
            try
            {
                var domainLogic = _logicFactory.Create(_uow.CurrentConnection);
                newItem = await domainLogic.PrepareCardForListAsync(selectedCard, isEdit);

                // Commit if everything succeeded
                await _uow.CommitAsync();
            }
            catch
            {
                // Roll back on any error
                await _uow.RollbackAsync();
                throw;
            }
            finally
            {
                // Tear down the connection
                await _uow.DisposeAsync();
            }

            // Now there is a fully-populated CardSet in newItem.
            // Run existing de-duplication logic in-memory:

            // skip if we already have this exact database ID
            if (newItem.CardId != null && targetCollection.Any(c => c.CardId == newItem.CardId))
            {
                return;
            }

            // otherwise skip if we match on the 4-tuple business key
            bool existsByKey = targetCollection.Any(c =>
                c.Uuid == newItem.Uuid &&
                c.SelectedFinish == newItem.SelectedFinish &&
                c.SelectedCondition == newItem.SelectedCondition &&
                c.Language == newItem.Language);

            if (existsByKey)
            {
                return;
            }

            // Finally, add it
            targetCollection.Add(newItem);
        }

        // Submitting new cards or card edits
        public async Task<List<CardChangeEventArgs>> SubmitNewCardsWithDefaultsBatchAsync(IEnumerable<CardSet> cards)
        {
            // For with defaults you know these are all new, so isEdit will be false,
            bool isEdit = cards.Any(c => c.CardId != null);

            // Open / Begin
            await _uow.BeginAsync();

            try
            {
                // Prepare each raw into a fully‐populated CardSet
                var prepared = new List<CardSet>();
                var domainLogic = _logicFactory.Create(_uow.CurrentConnection);

                foreach (var raw in cards)
                {
                    prepared.Add(await domainLogic.PrepareNewCardWithDefaultsAsync(raw));
                }

                // Hand off to your pure domain‐logic batch (no further UoW calls inside)
                var changes = await domainLogic.SaveBatchAsync(prepared, isEdit);

                // Commit on success
                await _uow.CommitAsync();

                // 5) Return a concrete List
                return [.. changes];
            }
            catch
            {
                // Roll back on any error
                await _uow.RollbackAsync();
                throw;
            }
            finally
            {
                // Clean up / close connection
                await _uow.DisposeAsync();
            }
        }
        public async Task<List<CardChangeEventArgs>> SubmitCardBatchAsync(IEnumerable<CardSet> cards)
        {
            bool isEdit = cards.Any(c => c.CardId != null);

            // Start transaction
            await _uow.BeginAsync();

            try
            {
                var domainLogic = _logicFactory.Create(_uow.CurrentConnection);
                var results = await domainLogic.SaveBatchAsync(cards, isEdit);

                await _uow.CommitAsync();
                return [.. results];
            }
            catch
            {
                // Rollback on any error
                await _uow.RollbackAsync();
                throw;
            }
            finally
            {
                // Tear down connection
                await _uow.DisposeAsync();
            }
        }
    }
}
