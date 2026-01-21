using CollectaMundo.ApplicationServices.EditCollection.Models;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.EditCollection;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.Infrastructure.EditCollection;
using CollectaMundo.Infrastructure.Shared;
using System.Collections.ObjectModel;

namespace CollectaMundo.ApplicationServices.EditCollection
{
    public class EditCollectionService(IDbConnectionFactory dbFactory, IEditCollectionLogic editLogic, IEditCollectionRepo repo) : IEditCollectionService
    {
        private readonly IDbConnectionFactory _dbFactory = dbFactory;
        private readonly IEditCollectionLogic _editLogic = editLogic;
        private readonly IEditCollectionRepo _repo = repo;

        // Adding cards to an add or edit listview
        public Task AddCardToAddCardsListViewAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection) => AddCardToListViewHelperAsync(selectedCard, targetCollection, false);
        public Task AddCardToEditCardsListViewAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection) => AddCardToListViewHelperAsync(selectedCard, targetCollection, true);
        private async Task AddCardToListViewHelperAsync(CardSet selectedCard, ObservableCollection<CardSet> targetCollection, bool isEdit)
        {
            CardSet newItem;

            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginAsync();

            try
            {
                // FETCH metadata here
                var finishes = await _repo.FetchFinishesForCardAsync(selectedCard.Uuid!, uow.CurrentConnection);
                var languages = await _repo.FetchLanguagesForCardAsync(selectedCard.Uuid!, uow.CurrentConnection);

                var metadata = new CardToAddMetadataDto { AvailableFinishes = finishes ?? [], AvailableLanguages = languages ?? [] };

                // DomainLogic now receives data instead of fetching it
                newItem = _editLogic.PrepareCardForList(selectedCard, metadata, isEdit);
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
        public async Task<CollectionChangeSet<CardSet>> SubmitNewCardsWithDefaultsBatchAsync(IEnumerable<CardSet> cards,ICollectionSnapshot snapshot)
        {
            // 1. Prepare cards (metadata fetch + pure logic)
            var prepared = new List<CardSet>();

            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginAsync();

            try
            {
                foreach (var raw in cards)
                {
                    var finishes = await _repo.FetchFinishesForCardAsync(
                        raw.Uuid!, uow.CurrentConnection);

                    var languages = await _repo.FetchLanguagesForCardAsync(
                        raw.Uuid!, uow.CurrentConnection);

                    var metadata = new CardToAddMetadataDto
                    {
                        AvailableFinishes = finishes ?? [],
                        AvailableLanguages = languages ?? []
                    };

                    var preparedCard = _editLogic.PrepareNewCardWithDefaults(raw, metadata);

                    prepared.Add(preparedCard);
                }

                // 2. PLAN using snapshot
                var plan = _editLogic.PlanBatch(prepared,snapshot,isEdit: false);

                // 3. Execute persistence plan
                foreach (var deleteId in plan.DeleteIds)
                {
                    await _repo.DeleteCardByIdAsync(deleteId, uow.CurrentConnection);
                }

                foreach (var update in plan.Updates)
                {
                    await _repo.UpdateCardFieldsByIdAsync(
                        update.CardId,
                        update.CardsOwned,
                        update.CardsForTrade,
                        update.Identity.Condition,
                        update.Identity.Language,
                        update.Identity.Finish,
                        uow.CurrentConnection);
                }

                foreach (var insert in plan.Inserts)
                {
                    var newId = await _repo.AddCardAndReturnIdAsync(
                        insert.Identity.Uuid,
                        insert.Identity.Condition,
                        insert.Identity.Language,
                        insert.Identity.Finish,
                        insert.CardsOwned,
                        insert.CardsForTrade,
                        uow.CurrentConnection);

                    insert.BindCardId(newId);
                }

                await uow.CommitAsync();

                // 4. Return the already-built change set
                return plan.ChangeSet;
            }
            catch
            {
                await uow.RollbackAsync();
                throw;
            }
        }
        public async Task<CollectionChangeSet<CardSet>> SubmitCardBatchAsync(IEnumerable<CardSet> cards, ICollectionSnapshot snapshot)
        {
            // 0. Determine intent
            var isEdit = cards.Any(c => c.CardId != null);

            // 1. Ask DomainLogic to PLAN the operation (pure, in-memory)
            // This returns:
            //  - what DB operations must occur
            //  - the final CollectionChangeSet<CardSet>
            var planResult = _editLogic.PlanBatch(cards, snapshot, isEdit);

            // 2. Execute persistence plan inside a UoW
            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginAsync();

            try
            {
                // 3a. Apply deletes
                foreach (var deleteId in planResult.DeleteIds)
                {
                    await _repo.DeleteCardByIdAsync(deleteId, uow.CurrentConnection);
                }

                // 3b. Apply updates
                foreach (var update in planResult.Updates)
                {
                    await _repo.UpdateCardFieldsByIdAsync(
                        update.CardId,
                        update.CardsOwned,
                        update.CardsForTrade,
                        update.Identity.Condition,
                        update.Identity.Language,
                        update.Identity.Finish,
                        uow.CurrentConnection);
                }

                // 3c. Apply inserts
                foreach (var insert in planResult.Inserts)
                {
                    var newId = await _repo.AddCardAndReturnIdAsync(
                        insert.Identity.Uuid,
                        insert.Identity.Condition,
                        insert.Identity.Language,
                        insert.Identity.Finish,
                        insert.CardsOwned,
                        insert.CardsForTrade,
                        uow.CurrentConnection);

                    insert.BindCardId(newId);
                }

                await uow.CommitAsync();
            }
            catch
            {
                await uow.RollbackAsync();
                throw;
            }

            // 4. Return the already-computed change set
            // MainWindowVM will apply it identically to Edit and Import
            return planResult.ChangeSet;
        }


    }
}
