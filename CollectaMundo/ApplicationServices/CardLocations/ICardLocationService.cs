using CollectaMundo.ApplicationServices.CardLocations.Models;
using CollectaMundo.DomainLogic.CardLocations.Models;
using CollectaMundo.DomainLogic.Decks.Models;

namespace CollectaMundo.ApplicationServices.CardLocations
{
    public interface ICardLocationService
    {
        // CREATE
        Task<CardLocationMutationResult> CreateLocationAsync(string name, CardLocationType type);
        Task<DeckManagementMutation> CreateDeckAsync(DeckManagementInput input);
        Task<IReadOnlyList<CardLocation>> CreateMissingLocationsAsStorageAsync(IReadOnlyList<string> names, CardLocationType type, CancellationToken token);

        // READ
        Task<IReadOnlyList<CardLocation>> GetAllLocationsAsync();
        Task<IReadOnlyList<DeckManagementRecord>> GetAllDecksAsync();

        // UPDATE
        Task<CardLocationMutationResult> UpdateLocationAsync(int id, string name, CardLocationType type);
        Task<DeckManagementMutation> UpdateDeckAsync(int locationId, DeckManagementInput input);

        // DELETE
        Task<CardLocationDeleteResult> DeleteLocationAsync(int id);
        Task<DeckManagementDeleteResult> DeleteDeckAsync(int locationId);
    }
}
