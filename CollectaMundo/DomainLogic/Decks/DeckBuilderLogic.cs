using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.DomainLogic.Shared;
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
                CanSetAsCompanion = CanBeCompanion(selectedCard)
            };
        }

        // Commander rules
        private static bool CanBeCommander(DeckBuildingRuleContext context, OracleCard card)
        {
            if (!IsCommanderLikeFormat(context.Format))
            {
                return false;
            }

            if (IsAlreadyInZone(context, card, DeckSection.Commander))
            {
                return false;
            }

            return IsLegendaryCreature(card) || RulesTextAllowsCommander(card);


            // Local helpers

            static bool IsCommanderLikeFormat(string? format)
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

            static bool IsLegendaryCreature(OracleCard card)
            {
                return CsvValues.Contains(card.SuperTypes, "Legendary") && CsvValues.Contains(card.Types, "Creature");
            }

            static bool IsAlreadyInZone(DeckBuildingRuleContext context, OracleCard card, DeckSection section)
            {
                return context.Entries.Any(x =>
                    x.Section == section &&
                    x.OracleId.Equals(card.ScryfallOracleId, StringComparison.OrdinalIgnoreCase));
            }

            static bool RulesTextAllowsCommander(OracleCard card)
            {
                var text = card.Text ?? string.Empty;

                return text.Contains("can be your commander", StringComparison.OrdinalIgnoreCase) || text.Contains("can be a commander", StringComparison.OrdinalIgnoreCase);
            }
        }

        // Companion rules
        private static bool CanBeCompanion(OracleCard card)
        {
            return HasKeyword(card, "Companion");
        }

        // Shared helpers
        private static bool HasKeyword(OracleCard card, string keyword)
        {
            return CsvValues.Contains(card.Keywords, keyword);
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
