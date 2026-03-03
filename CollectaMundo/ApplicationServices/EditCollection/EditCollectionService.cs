using CollectaMundo.ApplicationServices.EditCollection.Models;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.EditCollection;
using CollectaMundo.DomainLogic.EditCollection.Models;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.Infrastructure.EditCollection;
using CollectaMundo.Infrastructure.Shared;
using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.EditCollection
{
    public class EditCollectionService(IDbConnectionFactory dbFactory, IEditCollectionLogic editLogic, IEditCollectionRepo repo) : IEditCollectionService
    {
        private readonly IDbConnectionFactory _dbFactory = dbFactory;
        private readonly IEditCollectionLogic _editLogic = editLogic;
        private readonly IEditCollectionRepo _repo = repo;
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

                var prepared = _editLogic.PrepareCardForList(selectedCard, metadata, isEdit);

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
            var plan = _editLogic.PlanBatch(cards, snapshot, isEdit);

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

                    prepared.Add(_editLogic.PrepareNewCardWithDefaults(raw, metadata));
                }

                var plan = _editLogic.PlanBatch(prepared, snapshot, isEdit: false);

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
        private async Task ExecutePlanAsync(EditBatchPlan plan, SQLiteConnection connection)
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
    }
}
