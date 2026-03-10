using CollectaMundo.ApplicationServices.EditCollection.Models;
using CollectaMundo.ApplicationServices.Import.Models;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.ModifyCollection;
using CollectaMundo.DomainLogic.ModifyCollection.Models;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.Infrastructure.ModifyCollection;
using CollectaMundo.Infrastructure.Shared;
using CollectaMundo.ViewModels;
using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.ModifyCollection
{
    public class ModifyCollectionService(IDbConnectionFactory dbFactory, IModifyCollectionLogic logic, IModifyCollectionRepo repo) : IModifyCollectionService
    {
        private readonly IDbConnectionFactory _dbFactory = dbFactory;
        private readonly IModifyCollectionLogic _logic = logic;
        private readonly IModifyCollectionRepo _repo = repo;
        public Task<CardSet> CreateCardForAddAsync(CardSet selectedCard) => CreateCardForListAsync(selectedCard, isEdit: false);
        public Task<CardSet> CreateCardForEditAsync(CardSet selectedCard) => CreateCardForListAsync(selectedCard, isEdit: true);
        private async Task<CardSet> CreateCardForListAsync(CardSet selectedCard, bool isEdit)
        {
            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginAsync();

            try
            {
                var finishes = await _repo.FetchFinishesForCardAsync(selectedCard.Uuid!, uow.CurrentConnection);
                var languages = await _repo.FetchLanguagesForCardAsync(selectedCard.Uuid!, uow.CurrentConnection);

                var metadata = new CardToAddMetadataDto
                {
                    AvailableFinishes = finishes ?? [],
                    AvailableLanguages = languages ?? []
                };

                var prepared = _logic.PrepareCardForList(selectedCard, metadata, isEdit);

                await uow.CommitAsync();
                return prepared;
            }
            catch
            {
                await uow.RollbackAsync();
                throw;
            }
        }

        // Submitting new cards or card edits
        public async Task<CollectionChangeSet<CardSet>> SubmitCardBatchAsync(IEnumerable<CardSet> cards, ICollectionSnapshot snapshot)
        {
            var isEdit = cards.Any(c => c.CardId != null);
            var plan = _logic.PlanBatch(cards, snapshot, isEdit);

            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginAsync();

            try
            {
                await ExecutePlanAsync(plan, uow.CurrentConnection);
                await uow.CommitAsync();
                return plan.ChangeSet;
            }
            catch
            {
                await uow.RollbackAsync();
                throw;
            }
        }
        public async Task<CollectionChangeSet<CardSet>> SubmitNewCardsWithDefaultsBatchAsync(IEnumerable<CardSet> cards, ICollectionSnapshot snapshot)
        {
            var prepared = new List<CardSet>();

            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginAsync();

            try
            {
                foreach (var raw in cards)
                {
                    var finishes = await _repo.FetchFinishesForCardAsync(raw.Uuid!, uow.CurrentConnection);
                    var languages = await _repo.FetchLanguagesForCardAsync(raw.Uuid!, uow.CurrentConnection);

                    var metadata = new CardToAddMetadataDto
                    {
                        AvailableFinishes = finishes ?? [],
                        AvailableLanguages = languages ?? []
                    };

                    prepared.Add(_logic.PrepareNewCardWithDefaults(raw, metadata));
                }

                var plan = _logic.PlanBatch(prepared, snapshot, isEdit: false);

                await ExecutePlanAsync(plan, uow.CurrentConnection);
                await uow.CommitAsync();

                return plan.ChangeSet;
            }
            catch
            {
                await uow.RollbackAsync();
                throw;
            }
        }
        private async Task ExecutePlanAsync(ModifyBatchPlan plan, SQLiteConnection connection)
        {
            foreach (var deleteId in plan.DeleteIds)
            {
                await _repo.DeleteCardByIdAsync(deleteId, connection);
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
                    connection);
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
                    connection);

                insert.BindCardId(newId);
            }

#if DEBUG
            var unbound = plan.Inserts.Where(i => i.AssignedCardId is null).ToList();
            if (unbound.Count > 0)
            {
                throw new InvalidOperationException($"Unbound insert ids: {unbound.Count}");
            }
#endif
        }

        // Update in-memory collection after batch submission
        public CollectionChangeSet<CardSet> BuildCollectionChangeSet(CollectionMutation mutation, CardViewModel myCollection, CardViewModel allCards)
        {
            return _logic.BuildChangeSet(mutation, myCollection, allCards);
        }
        public void ApplyMyCollectionChanges(IList<CardSet> collection, CollectionChangeSet<CardSet> changes)
        {
            _logic.ApplyMyCollectionChanges(collection, changes);
        }
    }
}
