using CollectaMundo.ApplicationServices.CardLocations.Models;
using CollectaMundo.DomainLogic.CardLocations.Models;

namespace CollectaMundo.ApplicationServices.CardLocations
{
    public interface ICardLocationService
    {
        // CREATE
        Task<MutationResult<CardLocation>> CreateLocationAsync(string name, CardLocationType type);
        Task<MutationResult<DeckManagementRecord>> CreateDeckAsync(DeckManagementInput input);
        Task<IReadOnlyList<CardLocation>> CreateMissingLocationsAsStorageAsync(IReadOnlyList<string> names, CancellationToken token);

        // READ
        Task<IReadOnlyList<CardLocation>> GetAllLocationsAsync();
        Task<IReadOnlyList<DeckManagementRecord>> GetAllDecksAsync();

        // UPDATE
        Task<MutationResult<CardLocation>> UpdateLocationAsync(int id, string name, CardLocationType type);
        Task<MutationResult<DeckManagementRecord>> UpdateDeckAsync(int locationId, DeckManagementInput input);
        Task<IReadOnlyList<CardLocation>> UpdateLocationTypesAsync(IReadOnlyList<int> ids, CardLocationType type, CancellationToken token = default);

        // DELETE
        Task<CardLocationDeleteResult> DeleteLocationAsync(int id);
        Task<CardLocationDeleteResult> DeleteLocationsAsync(IReadOnlyList<int> ids, string entityName = "locations", CancellationToken token = default);
        Task<CardLocationDeleteResult> DeleteDeckAsync(int locationId);
    }
}
