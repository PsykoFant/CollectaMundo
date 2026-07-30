using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.DomainLogic.Shared.CardModels;

namespace CollectaMundo.DomainLogic.Decks
{
    public interface IDeckBuilderLogic
    {
        public DeckActionAvailability GetActionAvailability(DeckBuildingRuleContext context, OracleCard selectedCard);
        DeckCardValidationResult ValidateCard(DeckBuildingRuleContext context, DeckCardEntry entry, OracleCard oracleCard, ulong? formatMask);
        public DeckSlotPlacementResult GetCommanderPlacement(DeckBuildingRuleContext context, OracleCard selectedCard);
        public DeckSlotPlacementResult GetCompanionPlacement(DeckBuildingRuleContext context, OracleCard candidate);
    }
}
