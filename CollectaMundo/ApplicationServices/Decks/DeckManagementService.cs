using CollectaMundo.ApplicationServices.CardLocations;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardLocations.Models;
using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.Infrastructure.Decks;
using CollectaMundo.Infrastructure.Shared;

namespace CollectaMundo.ApplicationServices.Decks
{
    public sealed class DeckManagementService(IDbConnectionFactory dbFactory, IDeckManagementRepo deckManagementRepo,ICardLocationService cardLocationService) : IDeckManagementService
    {
        public async Task<IReadOnlyList<DeckManagementRecord>> GetAllAsync()
        {
            using var conn = await dbFactory.OpenConnectionAsync();

            return await deckManagementRepo.GetAllAsync(conn);
        }
        public async Task<DeckManagementMutation> CreateAsync(DeckManagementInput input)
        {
            var locationMutation = await cardLocationService.CreateAsync(input.Name, CardLocationType.Deck);

            if (locationMutation.Result.Code != OperationResultCode.Success || locationMutation.Location is null)
            {
                return new DeckManagementMutation
                {
                    Result = locationMutation.Result
                };
            }

            using var conn = await dbFactory.OpenConnectionAsync();

            await deckManagementRepo.UpsertMetadataAsync(
                conn,
                locationMutation.Location.Id,
                input.Format,
                input.Description);

            return new DeckManagementMutation
            {
                Result = new OperationResult(
                    OperationResultCode.Success,
                    "Deck created successfully."),

                Deck = new DeckManagementRecord
                {
                    LocationId = locationMutation.Location.Id,
                    Name = locationMutation.Location.Name,
                    Format = input.Format,
                    Description = input.Description
                }
            };
        }
        public async Task<DeckManagementMutation> UpdateAsync(int locationId, DeckManagementInput input)
        {
            var locationMutation = await cardLocationService.UpdateAsync(locationId, input.Name, CardLocationType.Deck);

            if (locationMutation.Result.Code != OperationResultCode.Success || locationMutation.Location is null)
            {
                return new DeckManagementMutation
                {
                    Result = locationMutation.Result
                };
            }

            using var conn = await dbFactory.OpenConnectionAsync();

            await deckManagementRepo.UpsertMetadataAsync(conn, locationId, input.Format, input.Description);

            return new DeckManagementMutation
            {
                Result = new OperationResult(OperationResultCode.Success, "Deck updated successfully."),

                Deck = new DeckManagementRecord
                {
                    LocationId = locationId,
                    Name = input.Name,
                    Format = input.Format,
                    Description = input.Description
                }
            };
        }
        public async Task<OperationResult> DeleteAsync(int locationId)
        {
            using var conn = await dbFactory.OpenConnectionAsync();

            await deckManagementRepo.DeleteMetadataAsync(conn, locationId);

            var locationMutation = await cardLocationService.DeleteAsync(locationId);

            return locationMutation.Result;
        }
    }
}
