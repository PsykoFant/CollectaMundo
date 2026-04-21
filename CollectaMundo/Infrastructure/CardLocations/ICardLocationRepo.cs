using CollectaMundo.DomainLogic.Shared.Models;
using System.Data.SQLite;

namespace CollectaMundo.Infrastructure.CardLocations
{
    public interface ICardLocationRepo
    {
        Task<IReadOnlyList<CardLocationRecord>> GetAllAsync(SQLiteConnection conn);
        Task<int> InsertAsync(SQLiteConnection conn, string name, string type);
        Task<int> UpdateAsync(SQLiteConnection conn, int id, string name, string type);
        Task<int> DeleteAsync(SQLiteConnection conn, int id);
        Task<IReadOnlyList<MyCollectionRow>> GetAllCollectionRowsAsync(SQLiteConnection conn);
        Task<IReadOnlyList<MyCollectionRow>> GetCollectionRowsByLocationIdAsync(SQLiteConnection conn, int locationId);
        Task<bool> ExistsByNameAsync(SQLiteConnection conn, string name, int? excludingId = null);
    }
}
