using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.DomainLogic.Decks.Models.BuildingRules;
using CollectaMundo.DomainLogic.Decks.Models.Enums;
using CollectaMundo.DomainLogic.Decks.Models.Stats;
using CollectaMundo.DomainLogic.Shared.CardModels;

namespace CollectaMundo.DomainLogic.Decks
{
    public interface IDeckBuilderLogic
    {
        DeckActionAvailability GetActionAvailability(DeckBuildingRuleContext context, OracleCard selectedCard);
        DeckCardValidationResult ValidateOracleCardLegallity(DeckBuildingRuleContext context, DeckCardEntry entry, OracleCard oracleCard, ulong? formatMask, ulong playablePrintingFormatsMask);
        DeckMutationResult MoveCard(IReadOnlyCollection<DeckCardState> cards, OracleCard card, DeckSection sourceSection, DeckSection destinationSection, int quantity);
        DeckSlotPlacementResult GetCommanderPlacement(DeckBuildingRuleContext context, OracleCard selectedCard);
        DeckSlotPlacementResult GetCompanionPlacement(DeckBuildingRuleContext context, OracleCard candidate);
        DeckStats CalculateDeckStats(IReadOnlyCollection<DeckCardState> cards);
    }
}
