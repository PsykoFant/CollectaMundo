using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.Decks.Models;

namespace CollectaMundo.ApplicationServices.Decks
{
    public interface IDeckManagementService
    {
        Task<IReadOnlyList<DeckManagementRecord>> GetAllAsync();
        Task<DeckManagementMutation> CreateAsync(DeckManagementInput input);
        Task<DeckManagementMutation> UpdateAsync(int locationId, DeckManagementInput input);
        Task<OperationResult> DeleteAsync(int locationId);
    }
}
