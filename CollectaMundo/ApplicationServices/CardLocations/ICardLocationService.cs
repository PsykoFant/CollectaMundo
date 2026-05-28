using CollectaMundo.ApplicationServices.CardLocations.Models;
using CollectaMundo.DomainLogic.CardLocations.Models;
using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.CardLocations
{
    public interface ICardLocationService
    {
		// CREATE
        Task<CardLocationMutationResult> CreateAsync(string name, CardLocationType type);		
		Task<DeckManagementMutation> CreateAsync(DeckManagementInput input);
        Task<CardLocationMutationResult> CreateCoreAsync(SQLiteConnection conn, SQLiteTransaction tx, string name, CardLocationType type);
        Task<IReadOnlyList<CardLocation>> CreateMissingLocationsAsStorageAsync(IReadOnlyList<string> names, CardLocationType type, CancellationToken token);
		
		// READ
        Task<IReadOnlyList<CardLocation>> GetAllAsync();
		Task<IReadOnlyList<DeckManagementRecord>> GetAllAsync();

        // UPDATE
        Task<CardLocationMutationResult> UpdateAsync(int id, string name, CardLocationType type);
        Task<DeckManagementMutation> UpdateAsync(int locationId, DeckManagementInput input);
		Task<CardLocationMutationResult> UpdateCoreAsync(SQLiteConnection conn, SQLiteTransaction tx, int id, string name, CardLocationType type);

		// DELETE
        Task<CardLocationDeleteResult> DeleteAsync(int id);
        Task<CardLocationDeleteResult> DeleteCoreAsync(SQLiteConnection conn, SQLiteTransaction tx, int id);
        Task<DeckManagementDeleteResult> DeleteAsync(int locationId);
	}
}
