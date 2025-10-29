using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.EditCollection;
using CollectaMundo.DomainLogic.EditCollection.Models;
using CollectaMundo.Infrastructure.Shared;
using System.Collections.ObjectModel;

namespace CollectaMundo.ApplicationServices.EditCollection
{
    public class EditCollectionService(IDbConnectionFactory dbFactory, IEditCollectionLogic editLogic) : IEditCollectionService
    {
        private readonly IDbConnectionFactory _dbFactory = dbFactory;
        private readonly IEditCollectionLogic _editLogic = editLogic;

        // Adding cards to an add or edit listview
        public Task AddCardToAddCardsListViewAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection) => AddCardToListViewHelperAsync(selectedCard, targetCollection, false);
        public Task AddCardToEditCardsListViewAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection) => AddCardToListViewHelperAsync(selectedCard, targetCollection, true);
        private async Task AddCardToListViewHelperAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection, bool isEdit)
        {
            CardSet newItem;

            // Start a UoW 
            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginAsync();
            try
            {
                newItem = await _editLogic.PrepareCardForListAsync(selectedCard, isEdit, uow.CurrentConnection);

                // Commit if everything succeeded
                await uow.CommitAsync();
            }
            catch
            {
                // Roll back on any error
                await uow.RollbackAsync();
                throw;
            }
            finally
            {
                // Tear down the connection
                await uow.DisposeAsync();
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
            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginAsync();

            try
            {
                // Prepare each raw into a fully‐populated CardSet
                var prepared = new List<CardSet>();

                foreach (var raw in cards)
                {
                    prepared.Add(await _editLogic.PrepareNewCardWithDefaultsAsync(raw, uow.CurrentConnection));
                }

                // Hand off to your pure domain‐logic batch (no further UoW calls inside)
                var changes = await _editLogic.SaveBatchAsync(prepared, isEdit, uow.CurrentConnection);

                // Commit on success
                await uow.CommitAsync();

                // 5) Return a concrete List
                return [.. changes];
            }
            catch
            {
                // Roll back on any error
                await uow.RollbackAsync();
                throw;
            }
            finally
            {
                // Clean up / close connection
                await uow.DisposeAsync();
            }
        }
        public async Task<List<CardChangeEventArgs>> SubmitCardBatchAsync(IEnumerable<CardSet> cards)
        {
            bool isEdit = cards.Any(c => c.CardId != null);

            // Start transaction
            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginAsync();

            try
            {
                var results = await _editLogic.SaveBatchAsync(cards, isEdit, uow.CurrentConnection);

                await uow.CommitAsync();
                return [.. results];
            }
            catch
            {
                // Rollback on any error
                await uow.RollbackAsync();
                throw;
            }
            finally
            {
                // Tear down connection
                await uow.DisposeAsync();
            }
        }
    }
}
