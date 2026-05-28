using CollectaMundo.ApplicationServices.CardLocations.Models;
using CollectaMundo.DomainLogic.CardLocations.Models;
using CollectaMundo.DomainLogic.Decks.Models;
using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.CardLocations
{
    public interface ICardLocationService
    {
        // CREATE
        Task<CardLocationMutationResult> CreateLocationAsync(string name, CardLocationType type);
        Task<DeckManagementMutation> CreateDeckAsync(DeckManagementInput input);
        Task<CardLocationMutationResult> CreateCoreAsync(SQLiteConnection conn, SQLiteTransaction tx, string name, CardLocationType type);
        Task<IReadOnlyList<CardLocation>> CreateMissingLocationsAsStorageAsync(IReadOnlyList<string> names, CardLocationType type, CancellationToken token);

        // READ
        Task<IReadOnlyList<CardLocation>> GetAllLocationsAsync();
        Task<IReadOnlyList<DeckManagementRecord>> GetAllDecksAsync();

        // UPDATE
        Task<CardLocationMutationResult> UpdateLocationAsync(int id, string name, CardLocationType type);
        Task<DeckManagementMutation> UpdateDeckAsync(int locationId, DeckManagementInput input);
        Task<CardLocationMutationResult> UpdateCoreAsync(SQLiteConnection conn, SQLiteTransaction tx, int id, string name, CardLocationType type);

        // DELETE
        Task<CardLocationDeleteResult> DeleteLocationAsync(int id);
        Task<CardLocationDeleteResult> DeleteCoreAsync(SQLiteConnection conn, SQLiteTransaction tx, int id);
        Task<DeckManagementDeleteResult> DeleteDeckAsync(int locationId);
    }
}
