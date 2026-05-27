using CollectaMundo.DomainLogic.Decks.Models;

namespace CollectaMundo.ApplicationServices.Decks
{
    public interface IDeckManagementService
    {
        // CRUD operations for deck management
        Task<DeckManagementMutation> CreateAsync(DeckManagementInput input);
        Task<IReadOnlyList<DeckManagementRecord>> GetAllAsync();
        Task<DeckManagementMutation> UpdateAsync(int locationId, DeckManagementInput input);
        Task<DeckManagementDeleteResult> DeleteAsync(int locationId);
    }
}
