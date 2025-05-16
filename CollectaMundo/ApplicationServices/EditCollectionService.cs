using CollectaMundo.DomainLogic;
using CollectaMundo.DomainLogic.Models;
using System.Collections.ObjectModel;

namespace CollectaMundo.ApplicationServices
{
    public class EditCollectionService(IUnitOfWork uow, IEditCollectionLogic domainLogic) : IEditCollectionService
    {
        private readonly IEditCollectionLogic _domainLogic = domainLogic ?? throw new ArgumentNullException(nameof(domainLogic));
        private readonly IUnitOfWork _uow = uow ?? throw new ArgumentNullException(nameof(uow));

        // Adding cards to an add or edit listview
        public Task AddCardToAddCardsListViewAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection) => AddCardToListViewHelperAsync(selectedCard, targetCollection, false);
        public Task AddCardToEditCardsListViewAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection) => AddCardToListViewHelperAsync(selectedCard, targetCollection, true);
        private async Task AddCardToListViewHelperAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection, bool isEdit)
        {
            CardSet newItem;

            // 1) Start a UoW (open/transaction)
            await _uow.BeginAsync();
            try
            {
                // 2) Do all your repo calls under that transaction
                //    (PrepareCardForListAsync will go to the repo for languages/finishes)
                newItem = await _domainLogic.PrepareCardForListAsync(selectedCard, isEdit);

                // 3) Commit if everything succeeded
                await _uow.CommitAsync();
            }
            catch
            {
                // 4) Roll back on any error
                await _uow.RollbackAsync();
                throw;
            }
            finally
            {
                // 5) Always tear down the connection
                await _uow.DisposeAsync();
            }

            // 6) Now there is a fully-populated CardSet in newItem.
            //    Run existing de-duplication logic in-memory:

            // skip if we already have this exact database ID
            if (newItem.CardId != null &&
                targetCollection.Any(c => c.CardId == newItem.CardId))
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
                return;

            // 7) finally, add it
            targetCollection.Add(newItem);
        }

        // Submitting new cards or card edits
        public async Task<List<CardChangeEventArgs>> SubmitNewCardsWithDefaultsBatchAsync(IEnumerable<CardSet> cards)
        {
            // (for “with defaults” you know these are all new, so isEdit will be false,
            // but we compute it here in case you ever reuse this method for mixed lists)
            bool isEdit = cards.Any(c => c.CardId != null);

            // 1) Open / Begin
            await _uow.BeginAsync();

            try
            {
                // 2) Prepare each raw into a fully‐populated CardSet
                var prepared = new List<CardSet>();
                foreach (var raw in cards)
                {
                    prepared.Add(await _domainLogic.PrepareNewCardWithDefaultsAsync(raw));
                }

                // 3) Hand off to your pure domain‐logic batch (no further UoW calls inside)
                var changes = await _domainLogic.SaveBatchAsync(prepared, isEdit);

                // 4) Commit on success
                await _uow.CommitAsync();

                // 5) Return a concrete List
                return changes.ToList();
            }
            catch
            {
                // 6) Roll back on any error
                await _uow.RollbackAsync();
                throw;
            }
            finally
            {
                // 7) Always clean up / close connection
                await _uow.DisposeAsync();
            }
        }
        public async Task<List<CardChangeEventArgs>> SubmitCardBatchAsync(IEnumerable<CardSet> cards)
        {
            bool isEdit = cards.Any(c => c.CardId != null);

            // 1) start transaction
            await _uow.BeginAsync();

            try
            {
                // 2) hand off to pure domain logic
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
