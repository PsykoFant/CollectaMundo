using CollectaMundo.ApplicationServices.Decks.Models;
using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.DomainLogic.Decks.Models.Enums;
using CollectaMundo.DomainLogic.Decks.Models.Records;
using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.ViewModels.Decks.Models.DragMoveViewRequests;

namespace CollectaMundo.ApplicationServices.Decks
{
    public interface IDeckBuilderService
    {
        Task<DeckMutationResult> AddCardsAsync(int deckLocationId, IReadOnlyCollection<DeckCardState> currentCards, IReadOnlyCollection<OracleCard> selectedCards, int quantity, DeckSection section);
        Task<DeckMutationResult> DeleteCardsAsync(int deckLocationId, IReadOnlyCollection<DeckCardState> currentCards, IReadOnlyCollection<DeckCardIdentityRecord> cardsToDelete);
        Task<DeckMutationResult> SetCardQuantityAsync(int deckLocationId, IReadOnlyCollection<DeckCardState> currentCards, DeckCardIdentityRecord card, int desiredQuantity);
        Task<DeckMutationResult> MoveCardsAsync(int deckLocationId, IReadOnlyCollection<DeckCardState> currentCards, IReadOnlyCollection<DeckCardMoveRequest> moves, DeckSection destinationSection);
        Task<DeckMutationResult> RemoveCardQuantitiesAsync(int deckLocationId, IReadOnlyCollection<DeckCardState> currentCards, IReadOnlyCollection<DeckCardQuantityRemoval> removals);
        public Task<IReadOnlyList<DeckCardEntry>> LoadDeckAsync(int locationId);
        DeckActionAvailability GetActionAvailability(string? format, IReadOnlyCollection<DeckCardState> deckCards, OracleCard selectedCard);
        DeckCardValidationResult ValidateCard(string? format, IReadOnlyCollection<DeckCardState> deckCards, DeckCardEntry entry, OracleCard oracleCard);
        Task<DeckMutationResult> SetCommanderAsync(int deckLocationId, string? format, IReadOnlyCollection<DeckCardState> currentCards, OracleCard selectedCard);
        Task<DeckMutationResult> SetCompanionAsync(int deckLocationId, string? format, IReadOnlyCollection<DeckCardState> currentCards, OracleCard selectedCard);
    }
}
