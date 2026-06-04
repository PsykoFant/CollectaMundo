using CollectaMundo.ApplicationServices.CardLocations.Models;
using CollectaMundo.DomainLogic.Shared.Models;
using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.CardLocations
{
    public interface ICardLocationRepo
    {
        // CREATE
        Task<int> CreateLocation(SQLiteConnection conn, SQLiteTransaction tx, string name, string type);
        Task<IReadOnlyList<CardLocationRecord>> CreateLocations(SQLiteConnection conn, SQLiteTransaction tx, IReadOnlyList<(string Name, string Type)> locations, CancellationToken token);
        Task UpsertMetadataAsync(SQLiteConnection conn, SQLiteTransaction tx, int locationId, string? format, string? description);

        // READ		
        Task<IReadOnlyList<CardLocationRecord>> GetAllLocationsAsync(SQLiteConnection conn, SQLiteTransaction? tx = null);
        Task<IReadOnlyList<int>> GetExistingLocationIdsAsync(SQLiteConnection conn, SQLiteTransaction tx, IReadOnlyList<int> ids, CancellationToken token = default);
        Task<IReadOnlyList<DeckManagementRecord>> GetAllDecksAsync(SQLiteConnection conn, SQLiteTransaction? tx = null);
        Task<IReadOnlyList<MyCollectionRow>> GetAllCollectionRowsAsync(SQLiteConnection conn, SQLiteTransaction tx);
        Task<IReadOnlyList<MyCollectionRow>> GetCollectionRowsByLocationIdsAsync(SQLiteConnection conn, SQLiteTransaction tx, IReadOnlyList<int> locationIds, CancellationToken token = default);
        Task<bool> ExistsByNameAsync(SQLiteConnection conn, SQLiteTransaction tx, string name, int? excludingId = null);

        // UPDATE
        Task<int> UpdateLocationAsync(SQLiteConnection conn, SQLiteTransaction tx, int id, string name, string type);
        Task<IReadOnlyList<CardLocationRecord>> UpdateLocationTypesAsync(SQLiteConnection conn, SQLiteTransaction tx, IReadOnlyList<int> ids, string type, CancellationToken token = default);

        // DELETE
        Task<int> DeleteLocationsAsync(SQLiteConnection conn, SQLiteTransaction tx, IReadOnlyList<int> ids, CancellationToken token = default);
        Task<int> DeleteDeckMetadataAsync(SQLiteConnection conn, SQLiteTransaction tx, int locationId);
        Task<int> DeleteDecksMetadataAsync(SQLiteConnection conn, SQLiteTransaction tx, IReadOnlyList<int> locationIds, CancellationToken token = default);
    }
}
