using CollectaMundo.ApplicationServices.CollectionMutations;
using CollectaMundo.ApplicationServices.EditCollection.Models;
using CollectaMundo.ApplicationServices.Shared.UnitOfWork;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.CollectionMutations.Models;
using CollectaMundo.DomainLogic.ModifyCollection;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.DomainLogic.Shared.Models;
using CollectaMundo.Infrastructure.ModifyCollection;
using CollectaMundo.Infrastructure.Shared.Models;
using System.Diagnostics;

namespace CollectaMundo.ApplicationServices.ModifyCollection
{
    public class ModifyCollectionService(IUnitOfWorkRunner uowRunner, IModifyCollectionLogic logic, IModifyCollectionRepo repo, ICollectionMutationsService mutationsService) : IModifyCollectionService
    {
        private readonly IUnitOfWorkRunner _uowRunner = uowRunner;
        private readonly IModifyCollectionLogic _logic = logic;
        private readonly IModifyCollectionRepo _repo = repo;
        private readonly ICollectionMutationsService _mutationsService = mutationsService;
        public async Task<CollectionCardDraft> CreateCardForListAsync(PrintingCard printing, CollectionCard? existingCollectionCard, bool isEdit)
        {
            return await _uowRunner.ExecuteReadOnlyAsync(async conn =>
            {
                var finishes = await _repo.FetchFinishesForCardAsync(printing.Uuid, conn);
                var languages = await _repo.FetchLanguagesForCardAsync(printing.Uuid, conn);

                var metadata = new CardToAddMetadataDto
                {
                    AvailableFinishes = finishes ?? [],
                    AvailableLanguages = languages ?? []
                };

                return _logic.PrepareCardForList(
                    printing,
                    existingCollectionCard,
                    metadata,
                    isEdit);
            });
        }

        // Submitting new cards or card edits
        public async Task<CollectionChangeSet<CollectionCardDbRow>> SubmitCardBatchAsync(IEnumerable<CollectionCardDraft> cards, ICollectionSnapshot snapshot)
        {
            try
            {
                var cardList = cards.ToList();

                return await _uowRunner.ExecuteWriteAsync(async (conn, tx) =>
                {
                    var changeSet = await _mutationsService.SubmitBatchAsync(cardList, snapshot, conn, tx);

                    return (Result: changeSet, Commit: true);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModifyCollectionService.SubmitCardBatchAsync] {ex}");
                throw;
            }
        }
        public async Task<CollectionChangeSet<CollectionCardDbRow>> SubmitNewCardsWithDefaultsBatchAsync(IEnumerable<PrintingCard> cards, ICollectionSnapshot snapshot)
        {
            return await _uowRunner.ExecuteWriteAsync(async (conn, tx) =>
            {
                var prepared = new List<CollectionCardDraft>();

                foreach (var printing in cards)
                {
                    var finishes = await _repo.FetchFinishesForCardAsync(printing.Uuid, conn);
                    var languages = await _repo.FetchLanguagesForCardAsync(printing.Uuid, conn);

                    var metadata = new CardToAddMetadataDto
                    {
                        AvailableFinishes = finishes ?? [],
                        AvailableLanguages = languages ?? []
                    };

                    prepared.Add(_logic.PrepareNewCardWithDefaults(printing, metadata));
                }

                var changeSet = await _mutationsService.SubmitBatchAsync(prepared, snapshot, conn, tx);

                return (Result: changeSet, Commit: true);
            });
        }
    }
}
