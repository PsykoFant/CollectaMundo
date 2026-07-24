using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.DomainLogic.Shared.CardModels;

namespace CollectaMundo.DomainLogic.Decks
{
    public sealed class DeckBuilderLogic : IDeckBuilderLogic
    {
        public DeckActionAvailability GetActionAvailability(DeckBuildingRuleContext context, OracleCard selectedCard)
        {
            return new DeckActionAvailability
            {
                CanSetAsCommander = GetCommanderPlacement(context, selectedCard).IsAllowed,
                CanSetAsCompanion = CanBeCompanion(selectedCard)
            };
        }

        // Commander rules
        private static readonly HashSet<string> CommanderLikeFormats = new(StringComparer.OrdinalIgnoreCase)
        {
            string.Empty,
            "casual",
            "commander",
            "duel",
            "predh",
            "brawl",
            "standardbrawl",
            "paupercommander",
            "oathbreaker",
            "tlr"
        };
        public CommanderPlacementResult GetCommanderPlacement(DeckBuildingRuleContext context, OracleCard selectedCard)
        {
            if (!IsCommanderLikeFormat(context.Format))
            {
                return NotAllowed("The selected format does not use commanders.");
            }

            if (!IsCommanderEligible(selectedCard))
            {
                return NotAllowed("The selected card cannot be a commander.");
            }

            var existingCommanders = context.Entries.Where(x => x.Section == DeckSection.Commander).ToList();

            if (existingCommanders.Any(x => string.Equals(x.Card.ScryfallOracleId, selectedCard.ScryfallOracleId, StringComparison.OrdinalIgnoreCase)))
            {
                return NotAllowed("The selected card is already a commander.");
            }

            if (existingCommanders.Count == 0)
            {
                return Allowed(CommanderPlacementAction.Add);
            }

            if (existingCommanders.Count == 1 && (AllowsAdditionalCommander(selectedCard) || AllowsAdditionalCommander(existingCommanders[0].Card)))
            {
                return Allowed(CommanderPlacementAction.Add);
            }

            return Allowed(CommanderPlacementAction.Replace);


            static CommanderPlacementResult Allowed(CommanderPlacementAction action)
            {
                return new CommanderPlacementResult
                {
                    Action = action
                };
            }

            static CommanderPlacementResult NotAllowed(string message)
            {
                return new CommanderPlacementResult
                {
                    Action = CommanderPlacementAction.NotAllowed,
                    Message = message
                };
            }
        }
        private static bool IsCommanderLikeFormat(string? format)
        {
            return format is not null && CommanderLikeFormats.Contains(format);
        }
        private static bool IsCommanderEligible(OracleCard card)
        {
            return IsLegendaryCreature(card) || RulesTextAllowsCommander(card) || IsBackground(card);
        }
        private static bool AllowsAdditionalCommander(OracleCard card)
        {
            return CsvValues.Contains(card.Keywords, "Partner")
                || CsvValues.Contains(card.Keywords, "Partner with")
                || CsvValues.Contains(card.Keywords, "Friends forever")
                || CsvValues.Contains(card.Keywords, "Doctor's Companion")
                || CsvValues.Contains(card.Keywords, "Choose a Background")
                || CsvValues.Contains(card.SubTypes, "Background");
        }
        private static bool IsLegendaryCreature(OracleCard card)
        {
            return CsvValues.Contains(card.SuperTypes, "Legendary") && CsvValues.Contains(card.Types, "Creature");
        }
        private static bool RulesTextAllowsCommander(OracleCard card)
        {
            var text = card.Text ?? string.Empty;

            return text.Contains("can be your commander", StringComparison.OrdinalIgnoreCase) || text.Contains("can be a commander", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsBackground(OracleCard card)
        {
            return CsvValues.Contains(card.SubTypes, "Background");
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
