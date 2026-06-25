using CollectaMundo.ApplicationServices.Decks.Models;
using CollectaMundo.Infrastructure.Shared.Models;
using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.CardLocations
{
    public interface ICardLocationRepo
    {
        // CREATE
        Task<int> CreateLocationAsync(SQLiteConnection conn, SQLiteTransaction tx, string name, string type);
        Task<IReadOnlyList<CardLocationDbRow>> CreateLocationsAsync(SQLiteConnection conn, SQLiteTransaction tx, IReadOnlyList<(string Name, string Type)> locations, CancellationToken token);
        Task UpsertMetadataAsync(SQLiteConnection conn, SQLiteTransaction tx, int locationId, string? format, string? description);

        // READ		
        Task<IReadOnlyList<CardLocationDbRow>> GetAllLocationsAsync(SQLiteConnection conn, SQLiteTransaction? tx = null);
        Task<IReadOnlyList<int>> GetExistingLocationIdsAsync(SQLiteConnection conn, SQLiteTransaction tx, IReadOnlyList<int> ids, CancellationToken token = default);
        Task<IReadOnlyList<DeckManagementRecord>> GetAllDecksAsync(SQLiteConnection conn, SQLiteTransaction? tx = null);
        Task<IReadOnlyList<string>> GetDeckFormatsAsync(SQLiteConnection conn, SQLiteTransaction? tx = null);
        Task<IReadOnlyList<CollectionCardDbRow>> GetAllCollectionRowsAsync(SQLiteConnection conn, SQLiteTransaction tx);
        Task<IReadOnlyList<CollectionCardDbRow>> GetCollectionRowsByLocationIdsAsync(SQLiteConnection conn, SQLiteTransaction tx, IReadOnlyList<int> locationIds, CancellationToken token = default);
        Task<bool> ExistsByNameAsync(SQLiteConnection conn, SQLiteTransaction tx, string name, int? excludingId = null);

        // UPDATE
        Task<int> UpdateLocationAsync(SQLiteConnection conn, SQLiteTransaction tx, int id, string name, string type);
        Task<IReadOnlyList<CardLocationDbRow>> UpdateLocationTypesAsync(SQLiteConnection conn, SQLiteTransaction tx, IReadOnlyList<int> ids, string type, CancellationToken token = default);
        Task<int> UpdateDeckFormatsAsync(SQLiteConnection conn, SQLiteTransaction tx, IReadOnlyList<int> locationIds, string format, CancellationToken token = default);

        // DELETE
        Task<int> DeleteLocationsAsync(SQLiteConnection conn, SQLiteTransaction tx, IReadOnlyList<int> ids, CancellationToken token = default);
        Task<int> DeleteDecksMetadataAsync(SQLiteConnection conn, SQLiteTransaction tx, IReadOnlyList<int> locationIds, CancellationToken token = default);
    }
}
