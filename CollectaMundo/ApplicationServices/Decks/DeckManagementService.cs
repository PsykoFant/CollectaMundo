using CollectaMundo.ApplicationServices.CardLocations;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardLocations.Models;
using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.Infrastructure.Decks;
using CollectaMundo.Infrastructure.Shared;

namespace CollectaMundo.ApplicationServices.Decks
{
    public sealed class DeckManagementService(IDbConnectionFactory dbFactory, IDeckManagementRepo deckManagementRepo, ICardLocationService cardLocationService) : IDeckManagementService
    {
        // CREATE
        public async Task<DeckManagementMutation> CreateAsync(DeckManagementInput input)
        {
            await using var uow = new UnitOfWork(dbFactory);
            await uow.BeginAsync();

            try
            {
                var locationMutation = await cardLocationService.CreateCoreAsync(uow.CurrentConnection, uow.CurrentTransaction, input.Name, CardLocationType.Deck);

                if (locationMutation.Result.Code != OperationResultCode.Success || locationMutation.Location is null)
                {
                    await uow.RollbackAsync();

                    return new DeckManagementMutation
                    {
                        Result = locationMutation.Result
                    };
                }

                await deckManagementRepo.UpsertMetadataAsync(uow.CurrentConnection, uow.CurrentTransaction, locationMutation.Location.Id, input.Format, input.Description);

                await uow.CommitAsync();

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
            using var conn = await dbFactory.OpenConnectionAsync();
            return await deckManagementRepo.GetAllAsync(conn);
        }

        // UPDATE
        public async Task<DeckManagementMutation> UpdateAsync(int locationId, DeckManagementInput input)
        {
            await using var uow = new UnitOfWork(dbFactory);
            await uow.BeginAsync();

            try
            {
                var locationMutation = await cardLocationService.UpdateCoreAsync(uow.CurrentConnection, uow.CurrentTransaction, locationId, input.Name, CardLocationType.Deck);

                if (locationMutation.Result.Code != OperationResultCode.Success || locationMutation.Location is null)
                {
                    await uow.RollbackAsync();

                    return new DeckManagementMutation
                    {
                        Result = locationMutation.Result
                    };
                }

                await deckManagementRepo.UpsertMetadataAsync(uow.CurrentConnection, uow.CurrentTransaction, locationId, input.Format, input.Description);

                await uow.CommitAsync();

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
        public async Task<OperationResult> DeleteAsync(int locationId)
        {
            await using var uow = new UnitOfWork(dbFactory);
            await uow.BeginAsync();

            try
            {
                await deckManagementRepo.DeleteMetadataAsync(uow.CurrentConnection, uow.CurrentTransaction, locationId);

                var locationMutation = await cardLocationService.DeleteCoreAsync(uow.CurrentConnection, uow.CurrentTransaction, locationId);

                if (locationMutation.Result.Code != OperationResultCode.Success)
                {
                    await uow.RollbackAsync();
                    return locationMutation.Result;
                }

                await uow.CommitAsync();

                return locationMutation.Result;
            }
            catch (Exception ex)
            {
                await uow.RollbackAsync();

                return new OperationResult(OperationResultCode.Error, $"Failed to delete deck: {ex.Message}");
            }
        }
    }
}
