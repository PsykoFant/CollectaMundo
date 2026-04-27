using CollectaMundo.ApplicationServices.CollectionMutations;
using CollectaMundo.ApplicationServices.EditCollection.Models;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.CollectionMutations;
using CollectaMundo.DomainLogic.ModifyCollection;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.DomainLogic.Shared.Models;
using CollectaMundo.Infrastructure.ModifyCollection;
using CollectaMundo.Infrastructure.Shared;
using System.Diagnostics;

namespace CollectaMundo.ApplicationServices.ModifyCollection
{
    public class ModifyCollectionService(IDbConnectionFactory dbFactory, IModifyCollectionLogic logic, IModifyCollectionRepo repo, ICollectionMutationsService mutationsService, ICollectionMutationsLogic mutationsLogic) : IModifyCollectionService
    {
        private readonly IDbConnectionFactory _dbFactory = dbFactory;
        private readonly IModifyCollectionLogic _logic = logic;
        private readonly IModifyCollectionRepo _repo = repo;
        private readonly ICollectionMutationsLogic _mutationsLogic = mutationsLogic;
        private readonly ICollectionMutationsService _mutationsService = mutationsService;
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
        public async Task<CollectionChangeSet<CardSet>> SubmitCardBatchAsync(IEnumerable<CardSet> cards,ICollectionSnapshot snapshot)
        {
            try
            {
                var cardList = cards.ToList();
                var isEdit = cardList.Any(c => c.CardId != null);
                var plan = _mutationsLogic.PlanIdentityRewriteBatch(cardList, snapshot);

                await using var uow = new UnitOfWork(_dbFactory);
                await uow.BeginAsync();

                try
                {
                    await _mutationsService.ExecutePlanAsync(plan, uow.CurrentConnection);
                    await uow.CommitAsync();
                    return plan.ChangeSet;
                }
                catch
                {
                    await uow.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModifyCollectionService.SubmitCardBatchAsync] {ex}");
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

                var plan = _mutationsLogic.PlanIdentityRewriteBatch(prepared, snapshot);

                await _mutationsService.ExecutePlanAsync(plan, uow.CurrentConnection);
                await uow.CommitAsync();

                return plan.ChangeSet;
            }
            catch
            {
                await uow.RollbackAsync();
                throw;
            }
        }
    }
}
