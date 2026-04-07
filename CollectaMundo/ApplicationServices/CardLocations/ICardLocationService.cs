using CollectaMundo.ApplicationServices.CardLocations.Models;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardLocations.Models;

namespace CollectaMundo.ApplicationServices.CardLocations
{
    public interface ICardLocationService
    {
        Task<IReadOnlyList<CardLocation>> GetAllAsync();
        Task<CardLocationMutationResult> CreateAsync(string name, CardLocationType type);
        Task<CardLocationMutationResult> UpdateAsync(int id, string name, CardLocationType type);
        Task<OperationResult> DeleteAsync(int id);
    }
}
