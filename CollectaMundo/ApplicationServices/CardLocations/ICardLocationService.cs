using CollectaMundo.ApplicationServices.CardLocations.Models;
using CollectaMundo.DomainLogic.CardLocations.Models;

namespace CollectaMundo.ApplicationServices.CardLocations
{
    public interface ICardLocationService
    {
        // CREATE
        Task<MutationResult<CardLocation>> CreateLocationAsync(string name, CardLocationType type);
        Task<MutationResult<DeckManagementRecord>> CreateDeckAsync(DeckManagementInput input);
        Task<IReadOnlyList<CardLocation>> CreateMissingLocationsAsStorageAsync(IReadOnlyList<string> names, CardLocationType type, CancellationToken token);

        // READ
        Task<IReadOnlyList<CardLocation>> GetAllLocationsAsync();
        Task<IReadOnlyList<DeckManagementRecord>> GetAllDecksAsync();

        // UPDATE
        Task<MutationResult<CardLocation>> UpdateLocationAsync(int id, string name, CardLocationType type);
        Task<MutationResult<DeckManagementRecord>> UpdateDeckAsync(int locationId, DeckManagementInput input);

        // DELETE
        Task<CardLocationDeleteResult> DeleteLocationAsync(int id);
        Task<CardLocationDeleteResult> DeleteDeckAsync(int locationId);
    }
}
