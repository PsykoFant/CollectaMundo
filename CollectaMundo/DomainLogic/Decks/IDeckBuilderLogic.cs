using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.DomainLogic.Decks.Models.Enums;
using CollectaMundo.DomainLogic.Decks.Models.Records;
using CollectaMundo.DomainLogic.Shared.CardModels;

namespace CollectaMundo.DomainLogic.Decks
{
    public interface IDeckBuilderLogic
    {
        DeckActionAvailability GetActionAvailability(DeckBuildingRuleContext context, OracleCard selectedCard);
        DeckCardValidationResult ValidateCard(DeckBuildingRuleContext context, DeckCardEntry entry, OracleCard oracleCard, ulong? formatMask);
        DeckMutationResult MoveCard(IReadOnlyCollection<DeckCardState> cards, OracleCard card, DeckSection sourceSection, DeckSection destinationSection, int quantity);
        DeckSlotPlacementResult GetCommanderPlacement(DeckBuildingRuleContext context, OracleCard selectedCard);
        DeckSlotPlacementResult GetCompanionPlacement(DeckBuildingRuleContext context, OracleCard candidate);
        DeckStats CalculateDeckStats(IReadOnlyCollection<DeckCardState> cards);
    }
}
