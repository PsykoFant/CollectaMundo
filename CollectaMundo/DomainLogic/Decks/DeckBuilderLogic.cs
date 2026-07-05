using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.DomainLogic.Shared.CardModels;

namespace CollectaMundo.DomainLogic.Decks
{
    public class DeckBuilderLogic : IDeckBuilderLogic
    {
        public DeckActionAvailability GetActionAvailability(DeckBuildingRuleContext context, OracleCard selectedCard)
        {
            return new DeckActionAvailability
            {
                CanSetAsCommander = CanBeCommander(context, selectedCard),
                CanSetAsCompanion = CanBeCompanion(context, selectedCard)
            };
        }

        // Commander rules
        private static bool CanBeCommander(DeckBuildingRuleContext context, OracleCard card)
        {
            if (!IsCommanderLikeFormat(context.Format))
            {
                return false;
            }

            return IsLegendaryCreature(card) || RulesTextAllowsCommander(card);

            static bool IsLegendaryCreature(OracleCard card)
            {
                return CsvContains(card.SuperTypes, "Legendary") && CsvContains(card.Types, "Creature");
            }
        }
        private static bool RulesTextAllowsCommander(OracleCard card)
        {
            var text = card.Text ?? string.Empty;

            return text.Contains("can be your commander", StringComparison.OrdinalIgnoreCase) || text.Contains("can be a commander", StringComparison.OrdinalIgnoreCase);
        }
        private static bool IsCommanderLikeFormat(string? format)
        {
            return format is not null &&
                   (
                       format.Equals(string.Empty) ||
                       format.Equals("casual", StringComparison.OrdinalIgnoreCase) ||
                       format.Equals("commander", StringComparison.OrdinalIgnoreCase) ||
                       format.Equals("duel", StringComparison.OrdinalIgnoreCase) ||
                       format.Equals("predh", StringComparison.OrdinalIgnoreCase) ||
                       format.Equals("brawl", StringComparison.OrdinalIgnoreCase) ||
                       format.Equals("standardbrawl", StringComparison.OrdinalIgnoreCase) ||
                       format.Equals("paupercommander", StringComparison.OrdinalIgnoreCase) ||
                       format.Equals("oathbreaker", StringComparison.OrdinalIgnoreCase) ||
                       format.Equals("tlr", StringComparison.OrdinalIgnoreCase)
                   );
        }

        // Companion rules
        private static bool CanBeCompanion(DeckBuildingRuleContext context, OracleCard card)
        {
            return HasKeyword(card, "Companion");
        }

        // Shared helpers
        private static bool HasKeyword(OracleCard card, string keyword)
        {
            return CsvContains(card.Keywords, keyword);
        }
        private static bool CsvContains(string? csv, string value)
        {
            return csv?
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Any(x => x.Equals(value, StringComparison.OrdinalIgnoreCase)) == true;
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
