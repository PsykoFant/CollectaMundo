using CollectaMundo.ApplicationServices.CollectionMutations;
using CollectaMundo.ApplicationServices.EditCollection.Models;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.CollectionMutations;
using CollectaMundo.DomainLogic.ModifyCollection;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.DomainLogic.Shared.Models;
using CollectaMundo.Infrastructure.ModifyCollection;
using System.Diagnostics;

namespace CollectaMundo.ApplicationServices.ModifyCollection
{
    public class ModifyCollectionService(IUnitOfWorkRunner uowRunner, IModifyCollectionLogic logic, IModifyCollectionRepo repo, ICollectionMutationsService mutationsService, ICollectionMutationsLogic mutationsLogic) : IModifyCollectionService
    {
        private readonly IUnitOfWorkRunner _uowRunner = uowRunner;
        private readonly IModifyCollectionLogic _logic = logic;
        private readonly IModifyCollectionRepo _repo = repo;
        private readonly ICollectionMutationsLogic _mutationsLogic = mutationsLogic;
        private readonly ICollectionMutationsService _mutationsService = mutationsService;
        public async Task<CardSet> CreateCardForListAsync(CardSet selectedCard, bool isEdit)
        {
            return await _uowRunner.ExecuteReadOnlyAsync(async conn =>
            {
                var finishes = await _repo.FetchFinishesForCardAsync(selectedCard.Uuid!, conn);
                var languages = await _repo.FetchLanguagesForCardAsync(selectedCard.Uuid!, conn);

                var metadata = new CardToAddMetadataDto
                {
                    AvailableFinishes = finishes ?? [],
                    AvailableLanguages = languages ?? []
                };

                return _logic.PrepareCardForList(selectedCard, metadata, isEdit);
            });
        }

        // Submitting new cards or card edits
        public async Task<CollectionChangeSet<CardSet>> SubmitCardBatchAsync(IEnumerable<CardSet> cards, ICollectionSnapshot snapshot)
        {
            try
            {
                var cardList = cards.ToList();
                var plan = _mutationsLogic.PlanIdentityRewriteBatch(cardList, snapshot);

                return await _uowRunner.ExecuteWriteAsync(async (conn, tx) =>
                {
                    await _mutationsService.ExecutePlanAsync(plan, conn, tx);
                    return (Result: plan.ChangeSet, Commit: true);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModifyCollectionService.SubmitCardBatchAsync] {ex}");
                throw;
            }
        }
        public async Task<CollectionChangeSet<CardSet>> SubmitNewCardsWithDefaultsBatchAsync(IEnumerable<CardSet> cards, ICollectionSnapshot snapshot)
        {
            return await _uowRunner.ExecuteWriteAsync(async (conn, tx) =>
            {
                var prepared = new List<CardSet>();

                foreach (var raw in cards)
                {
                    var finishes = await _repo.FetchFinishesForCardAsync(raw.Uuid!, conn);
                    var languages = await _repo.FetchLanguagesForCardAsync(raw.Uuid!, conn);

                    var metadata = new CardToAddMetadataDto
                    {
                        AvailableFinishes = finishes ?? [],
                        AvailableLanguages = languages ?? []
                    };

                    prepared.Add(_logic.PrepareNewCardWithDefaults(raw, metadata));
                }

                var plan = _mutationsLogic.PlanIdentityRewriteBatch(prepared, snapshot);

                await _mutationsService.ExecutePlanAsync(plan, conn, tx);

                return (Result: plan.ChangeSet, Commit: true);
            });
        }
    }
}
