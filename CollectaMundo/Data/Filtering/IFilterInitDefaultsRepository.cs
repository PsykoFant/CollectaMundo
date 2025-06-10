using CollectaMundo.DomainLogic.Filtering.Models;
using System.Data.SQLite;

namespace CollectaMundo.Data
{
    public interface IFilterInitDefaultsRepository
    {
        Task<List<FilterDefaults>> GetFilterDefaultsAsync(SQLiteConnection connection);
    }
}
