using CollectaMundo.DomainLogic.Filtering.Models;

namespace CollectaMundo.Data.Filtering
{
    public interface IFilterInitDefaultsRepository
    {
        Task<List<FilterDefaults>> GetFilterDefaultsAsync();
    }
}
