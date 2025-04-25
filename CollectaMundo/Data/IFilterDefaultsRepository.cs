using CollectaMundo.Models;

namespace CollectaMundo.Data
{
    public interface IFilterDefaultsRepository
    {
        Task<List<FilterDefaults>> GetFilterDefaultsAsync();
    }
}
