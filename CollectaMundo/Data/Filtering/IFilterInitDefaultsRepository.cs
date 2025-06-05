using CollectaMundo.DomainLogic.Filtering.Models;

namespace CollectaMundo.Data
{
    public interface IFilterInitDefaultsRepository
    {
        Task<List<FilterDefaults>> GetFilterDefaultsAsync();
    }
}
