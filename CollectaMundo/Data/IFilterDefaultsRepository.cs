using CollectaMundo.DomainLogic.Models;

namespace CollectaMundo.Data
{
    public interface IFilterDefaultsRepository
    {
        Task<List<FilterDefaults>> GetFilterDefaultsAsync();
    }
}
