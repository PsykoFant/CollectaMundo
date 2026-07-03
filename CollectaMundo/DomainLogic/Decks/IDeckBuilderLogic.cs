using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.DomainLogic.Shared.CardModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CollectaMundo.DomainLogic.Decks
{
    public interface IDeckBuilderLogic
    {
        public DeckActionAvailability GetActionAvailability(DeckBuildingRuleContext context,OracleCard selectedCard);
        public DeckCardValidationResult ValidateCard(DeckBuildingRuleContext context,DeckCardEntry entry,OracleCard oracleCard);
    }
}
