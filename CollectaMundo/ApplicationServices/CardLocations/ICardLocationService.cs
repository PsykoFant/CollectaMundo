using CollectaMundo.ApplicationServices.CardLocations.Models;
using CollectaMundo.DomainLogic.CardLocations.Models;

namespace CollectaMundo.ApplicationServices.CardLocations
{
    public interface ICardLocationService
    {
        Task<IReadOnlyList<CardLocation>> GetAllAsync();
        Task<CardLocationMutationResult> CreateAsync(string name, CardLocationType type);
        Task<IReadOnlyList<CardLocation>> CreateMissingAsync(IReadOnlyList<string> names, CardLocationType type, CancellationToken token);
        Task<CardLocationMutationResult> UpdateAsync(int id, string name, CardLocationType type);
        Task<CardLocationDeleteResult> DeleteAsync(int id);
    }
}
