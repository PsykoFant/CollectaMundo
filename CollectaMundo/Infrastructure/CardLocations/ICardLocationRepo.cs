using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.DomainLogic.Shared.Models;
using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.CardLocations
{
    public interface ICardLocationRepo
    {
        // CREATE
        Task<int> InsertAsync(SQLiteConnection conn, SQLiteTransaction tx, string name, string type);
        Task<IReadOnlyList<CardLocationRecord>> InsertManyAsync(SQLiteConnection conn, SQLiteTransaction tx, IReadOnlyList<(string Name, string Type)> locations, CancellationToken token);
        Task UpsertMetadataAsync(SQLiteConnection conn, SQLiteTransaction tx, int locationId, string? format, string? description);

        // READ		
        Task<IReadOnlyList<CardLocationRecord>> GetAllLocationsAsync(SQLiteConnection conn, SQLiteTransaction? tx = null);
        Task<IReadOnlyList<DeckManagementRecord>> GetAllDecksAsync(SQLiteConnection conn);
        Task<IReadOnlyList<MyCollectionRow>> GetAllCollectionRowsAsync(SQLiteConnection conn, SQLiteTransaction tx);
        Task<IReadOnlyList<MyCollectionRow>> GetCollectionRowsByLocationIdAsync(SQLiteConnection conn, SQLiteTransaction tx, int locationId);
        Task<bool> ExistsByNameAsync(SQLiteConnection conn, SQLiteTransaction tx, string name, int? excludingId = null);
        Task<bool> ExistsByIdAsync(SQLiteConnection conn, SQLiteTransaction tx, int id);

        // UPDATE
        Task<int> UpdateAsync(SQLiteConnection conn, SQLiteTransaction tx, int id, string name, string type);

        // DELETE
        Task<int> DeleteAsync(SQLiteConnection conn, SQLiteTransaction tx, int id);
        Task<int> DeleteDeckMetadataAsync(SQLiteConnection conn, SQLiteTransaction tx, int locationId);
    }
}
