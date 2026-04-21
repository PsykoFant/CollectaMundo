using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CollectaMundo.Infrastructure.CardLocations
{
    public interface ICardLocationRepo
    {
        Task<IReadOnlyList<CardLocationRecord>> GetAllAsync(SQLiteConnection conn);
        Task<int> InsertAsync(SQLiteConnection conn, string name, string type);
        Task<int> UpdateAsync(SQLiteConnection conn, int id, string name, string type);
        Task<int> ClearLocationFromCollectionAsync(SQLiteConnection conn, int locationId);
        Task<int> DeleteAsync(SQLiteConnection conn, int id);
        Task<bool> ExistsByNameAsync(SQLiteConnection conn, string name, int? excludingId = null);
    }
}
