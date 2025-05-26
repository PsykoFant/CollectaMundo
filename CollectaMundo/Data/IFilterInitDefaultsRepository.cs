using CollectaMundo.DomainLogic.Models;

namespace CollectaMundo.Data
{
    public interface IFilterInitDefaultsRepository
    {
        Task<List<FilterDefaults>> GetFilterDefaultsAsync();
    }
}
