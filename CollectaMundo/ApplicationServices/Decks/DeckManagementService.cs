using CollectaMundo.ApplicationServices.CardLocations;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardLocations.Models;
using CollectaMundo.DomainLogic.Decks.Models;

namespace CollectaMundo.ApplicationServices.Decks
{
    public sealed class DeckManagementService(ICardLocationService cardLocationService) : IDeckManagementService
    {
        public Task<IReadOnlyList<DeckManagementRecord>> GetAllAsync()
        {
            IReadOnlyList<DeckManagementRecord> decks = [];
            return Task.FromResult(decks);
        }

        public async Task<DeckManagementMutation> CreateAsync(DeckManagementInput input)
        {
            var locationMutation = await cardLocationService.CreateAsync(input.Name, CardLocationType.Deck);

            if (locationMutation.Result.Code != OperationResultCode.Success || locationMutation.Location is null)
            {
                return new DeckManagementMutation
                {
                    Result = locationMutation.Result,
                    Deck = null
                };
            }

            var deck = new DeckManagementRecord
            {
                LocationId = locationMutation.Location.Id,
                Name = locationMutation.Location.Name,
                Format = input.Format,
                Description = input.Description
            };

            return new DeckManagementMutation
            {
                Result = new OperationResult(OperationResultCode.Success, "Deck created successfully."),
                Deck = deck
            };
        }
        public async Task<DeckManagementMutation> UpdateAsync(int locationId, DeckManagementInput input)
        {
            var locationMutation = await cardLocationService.UpdateAsync(locationId, input.Name, CardLocationType.Deck);

            if (locationMutation.Result.Code != OperationResultCode.Success || locationMutation.Location is null)
            {
                return new DeckManagementMutation
                {
                    Result = locationMutation.Result,
                    Deck = null
                };
            }

            var deck = new DeckManagementRecord
            {
                LocationId = locationMutation.Location.Id,
                Name = locationMutation.Location.Name,
                Format = input.Format,
                Description = input.Description
            };

            return new DeckManagementMutation
            {
                Result = new OperationResult(OperationResultCode.Success, "Deck updated successfully."),
                Deck = deck
            };
        }
        public async Task<OperationResult> DeleteAsync(int locationId)
        {
            var locationDelete = await cardLocationService.DeleteAsync(locationId);

            return locationDelete.Result;
        }
    }
}
