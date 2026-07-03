using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.DomainLogic.Shared.CardModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CollectaMundo.DomainLogic.Decks
{
    public class DeckBuilderLogic : IDeckBuilderLogic
    {
        public DeckActionAvailability GetActionAvailability(DeckBuildingRuleContext context, OracleCard selectedCard)
        {
            // Implement logic to determine if the selected card can be set as Commander or Companion
            bool canSetAsCommander = false;
            bool canSetAsCompanion = false;
            // Example logic (to be replaced with actual rules)
            if (selectedCard.SuperTypes?.Contains("Legendary") == true && selectedCard.Types?.Contains("Creature") == true)
            {
                canSetAsCommander = true;
            }
            if (selectedCard.Keywords?.Contains("Companion") == true)
            {
                canSetAsCompanion = true;
            }
            return new DeckActionAvailability
            {
                CanSetAsCommander = canSetAsCommander,
                CanSetAsCompanion = canSetAsCompanion
            };
        }
        public DeckCardValidationResult ValidateCard(DeckBuildingRuleContext context, DeckCardEntry entry, OracleCard oracleCard)
        {
            return new DeckCardValidationResult
            {
                IsLegal = true,
                Message = string.Empty
            };
        }
    }
}
