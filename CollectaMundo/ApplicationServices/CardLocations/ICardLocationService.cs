using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardLocations.Models;

namespace CollectaMundo.ApplicationServices.CardLocations
{
    public interface ICardLocationService
    {
        Task<IReadOnlyList<CardLocation>> GetAllAsync();
        Task<OperationResult> CreateAsync(string name, CardLocationType type);
        Task<OperationResult> UpdateAsync(int id, string name, CardLocationType type);
        Task<OperationResult> DeleteAsync(int id);
    }
}
