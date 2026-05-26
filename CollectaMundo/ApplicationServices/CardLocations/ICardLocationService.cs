using CollectaMundo.ApplicationServices.CardLocations.Models;
using CollectaMundo.DomainLogic.CardLocations.Models;
using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices.CardLocations
{
    public interface ICardLocationService
    {
        Task<IReadOnlyList<CardLocation>> GetAllAsync();

        // Standalone operations used by CardLocationViewModel
        Task<CardLocationMutationResult> CreateAsync(string name, CardLocationType type);
        Task<CardLocationMutationResult> UpdateAsync(int id, string name, CardLocationType type);
        Task<CardLocationDeleteResult> DeleteAsync(int id);

        // Composable operations used by other services
        Task<CardLocationMutationResult> CreateCoreAsync(SQLiteConnection conn, SQLiteTransaction tx, string name, CardLocationType type);
        Task<IReadOnlyList<CardLocation>> CreateMissingLocationsAsStorageAsync(IReadOnlyList<string> names, CardLocationType type, CancellationToken token);
        Task<CardLocationMutationResult> UpdateCoreAsync(SQLiteConnection conn, SQLiteTransaction tx, int id, string name, CardLocationType type);
        Task<CardLocationDeleteResult> DeleteCoreAsync(SQLiteConnection conn, SQLiteTransaction tx, int id);
    }
}
