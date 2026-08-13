using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.DomainLogic.Decks.Models.Enums;
using CollectaMundo.DomainLogic.Decks.Models.Records;
using CollectaMundo.DomainLogic.Shared.CardModels;

namespace CollectaMundo.ApplicationServices.Decks
{
    public interface IDeckBuilderService
    {
        Task<DeckMutationResult> AddCardsAsync(int deckLocationId, IReadOnlyCollection<DeckCardState> currentCards, IReadOnlyCollection<OracleCard> selectedCards, int quantity, DeckSection section);
        Task<DeckMutationResult> DeleteCardsAsync(int deckLocationId, IReadOnlyCollection<DeckCardState> currentCards, IReadOnlyCollection<DeckCardIdentityRecord> cardsToDelete);
        Task<DeckMutationResult> SetCardQuantityAsync(int deckLocationId, IReadOnlyCollection<DeckCardState> currentCards, DeckCardIdentityRecord card, int desiredQuantity);
        Task<DeckMutationResult> MoveCardAsync(int deckLocationId, IReadOnlyCollection<DeckCardState> currentCards, OracleCard card, DeckSection sourceSection, DeckSection destinationSection, int quantity);
        public Task<IReadOnlyList<DeckCardEntry>> LoadDeckAsync(int locationId);
        DeckActionAvailability GetActionAvailability(string? format, IReadOnlyCollection<DeckCardState> deckCards, OracleCard selectedCard);
        DeckCardValidationResult ValidateCard(string? format, IReadOnlyCollection<DeckCardState> deckCards, DeckCardEntry entry, OracleCard oracleCard);
        Task<DeckMutationResult> SetCommanderAsync(int deckLocationId, string? format, IReadOnlyCollection<DeckCardState> currentCards, OracleCard selectedCard);
        Task<DeckMutationResult> SetCompanionAsync(int deckLocationId, string? format, IReadOnlyCollection<DeckCardState> currentCards, OracleCard selectedCard);
    }
}
