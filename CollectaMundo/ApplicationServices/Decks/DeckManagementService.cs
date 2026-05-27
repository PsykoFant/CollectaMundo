using CollectaMundo.ApplicationServices.CardLocations;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.CardLocations.Models;
using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.DomainLogic.Shared.Models;
using CollectaMundo.Infrastructure.Decks;
using CollectaMundo.Infrastructure.Shared;

namespace CollectaMundo.ApplicationServices.Decks
{
    public sealed class DeckManagementService(IDbConnectionFactory dbFactory, IDeckManagementRepo deckManagementRepo, ICardLocationService cardLocationService, ICardLocationLookupStore cardLocationLookupStore) : IDeckManagementService
    {
        private readonly IDbConnectionFactory _dbFactory = dbFactory;
        private readonly IDeckManagementRepo _deckManagementRepo = deckManagementRepo;
        private readonly ICardLocationService _cardLocationService = cardLocationService;
        private readonly ICardLocationLookupStore _cardLocationLookupStore = cardLocationLookupStore;

        // CREATE
        public async Task<DeckManagementMutation> CreateAsync(DeckManagementInput input)
        {
            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginAsync();

            try
            {
                var locationMutation = await _cardLocationService.CreateCoreAsync(uow.CurrentConnection, uow.CurrentTransaction, input.Name, CardLocationType.Deck);

                if (locationMutation.Result.Code != OperationResultCode.Success || locationMutation.Location is null)
                {
                    await uow.RollbackAsync();

                    return new DeckManagementMutation
                    {
                        Result = locationMutation.Result
                    };
                }

                await _deckManagementRepo.UpsertMetadataAsync(uow.CurrentConnection, uow.CurrentTransaction, locationMutation.Location.Id, input.Format, input.Description);

                await uow.CommitAsync();

                _cardLocationLookupStore.Upsert(locationMutation.Location);

                return new DeckManagementMutation
                {
                    Result = new OperationResult(OperationResultCode.Success, "Deck created successfully."),
                    Deck = new DeckManagementRecord
                    {
                        LocationId = locationMutation.Location.Id,
                        Name = locationMutation.Location.Name,
                        Format = input.Format,
                        Description = input.Description
                    }
                };
            }
            catch (Exception ex)
            {
                await uow.RollbackAsync();

                return new DeckManagementMutation
                {
                    Result = new OperationResult(OperationResultCode.Error, $"Failed to create deck: {ex.Message}")
                };
            }
        }

        // READ
        public async Task<IReadOnlyList<DeckManagementRecord>> GetAllAsync()
        {
            using var conn = await _dbFactory.OpenConnectionAsync();
            return await _deckManagementRepo.GetAllAsync(conn);
        }

        // UPDATE
        public async Task<DeckManagementMutation> UpdateAsync(int locationId, DeckManagementInput input)
        {
            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginAsync();

            try
            {
                var locationMutation = await _cardLocationService.UpdateCoreAsync(uow.CurrentConnection, uow.CurrentTransaction, locationId, input.Name, CardLocationType.Deck);

                if (locationMutation.Result.Code != OperationResultCode.Success || locationMutation.Location is null)
                {
                    await uow.RollbackAsync();

                    return new DeckManagementMutation
                    {
                        Result = locationMutation.Result
                    };
                }

                await _deckManagementRepo.UpsertMetadataAsync(uow.CurrentConnection, uow.CurrentTransaction, locationId, input.Format, input.Description);

                await uow.CommitAsync();

                _cardLocationLookupStore.Upsert(locationMutation.Location);

                return new DeckManagementMutation
                {
                    Result = new OperationResult(OperationResultCode.Success, "Deck updated successfully."),
                    Deck = new DeckManagementRecord
                    {
                        LocationId = locationId,
                        Name = locationMutation.Location.Name,
                        Format = input.Format,
                        Description = input.Description
                    }
                };
            }
            catch (Exception ex)
            {
                await uow.RollbackAsync();

                return new DeckManagementMutation
                {
                    Result = new OperationResult(OperationResultCode.Error, $"Failed to update deck: {ex.Message}")
                };
            }
        }

        // DELETE
        public async Task<DeckManagementDeleteResult> DeleteAsync(int locationId)
        {
            await using var uow = new UnitOfWork(_dbFactory);
            await uow.BeginAsync();

            try
            {
                var locationMutation = await _cardLocationService.DeleteCoreAsync(uow.CurrentConnection, uow.CurrentTransaction, locationId);

                if (locationMutation.Result.Code != OperationResultCode.Success)
                {
                    await uow.RollbackAsync();

                    return new DeckManagementDeleteResult(locationMutation.Result, locationMutation.CollectionChangeSet);
                }

                await uow.CommitAsync();

                _cardLocationLookupStore.Remove(locationId);

                return new DeckManagementDeleteResult(locationMutation.Result, locationMutation.CollectionChangeSet);
            }
            catch (Exception ex)
            {
                await uow.RollbackAsync();

                return new DeckManagementDeleteResult(new OperationResult(OperationResultCode.Error, $"Failed to delete deck: {ex.Message}"), new CollectionChangeSet<CardSet>());
            }
        }
    }
}
