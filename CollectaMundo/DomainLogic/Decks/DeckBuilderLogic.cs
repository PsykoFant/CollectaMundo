using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.DomainLogic.Decks.Models.Enums;
using CollectaMundo.DomainLogic.Decks.Models.Records;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.DomainLogic.Shared.CardModels;

namespace CollectaMundo.DomainLogic.Decks
{
    public sealed class DeckBuilderLogic : IDeckBuilderLogic
    {
        public DeckActionAvailability GetActionAvailability(DeckBuildingRuleContext context, OracleCard selectedCard)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(selectedCard);

            return new DeckActionAvailability
            {
                CanSetAsCommander = GetCommanderPlacement(context, selectedCard).IsAllowed,
                CanSetAsCompanion = GetCompanionPlacement(context, selectedCard).IsAllowed
            };
        }
        public DeckCardValidationResult ValidateCard(DeckBuildingRuleContext context, DeckCardEntry entry, OracleCard oracleCard, ulong? formatMask)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(entry);
            ArgumentNullException.ThrowIfNull(oracleCard);

            var isLegal = oracleCard.GamePlayCard != 1 || (formatMask.HasValue && (oracleCard.PlayableFormatsMask & formatMask.Value) != 0);

            return new DeckCardValidationResult
            {
                IsLegal = isLegal,
                Message = isLegal
                    ? string.Empty
                    : $"{oracleCard.Name} is not legal in {context.Format}."
            };
        }
        public DeckMutationResult MoveCard(IReadOnlyCollection<DeckCardState> cards, OracleCard card, DeckSection sourceSection, DeckSection destinationSection, int quantity)
        {
            if (quantity <= 0)
            {
                return new DeckMutationResult
                {
                    Succeeded = false,
                    Message = "Quantity must be greater than zero.",
                    Cards = [.. cards]
                };
            }

            if (sourceSection == destinationSection)
            {
                return new DeckMutationResult
                {
                    Succeeded = false,
                    Message = "Source and destination zones are the same.",
                    Cards = [.. cards]
                };
            }

            var source = cards.FirstOrDefault(x => x.Section == sourceSection && string.Equals(x.Card.ScryfallOracleId, card.ScryfallOracleId, StringComparison.OrdinalIgnoreCase));

            if (source is null)
            {
                return new DeckMutationResult
                {
                    Succeeded = false,
                    Message = "Card was not found in the source zone.",
                    Cards = [.. cards]
                };
            }

            if (source.DesiredQuantity < quantity)
            {
                return new DeckMutationResult
                {
                    Succeeded = false,
                    Message = "Source zone does not contain enough copies.",
                    Cards = [.. cards]
                };
            }

            var destination = cards.FirstOrDefault(x => x.Section == destinationSection && string.Equals(x.Card.ScryfallOracleId, card.ScryfallOracleId, StringComparison.OrdinalIgnoreCase));

            var result = new List<DeckCardState>();

            foreach (var existing in cards)
            {
                var isSource = existing.Section == sourceSection && string.Equals(existing.Card.ScryfallOracleId, card.ScryfallOracleId, StringComparison.OrdinalIgnoreCase);
                var isDestination = existing.Section == destinationSection && string.Equals(existing.Card.ScryfallOracleId, card.ScryfallOracleId, StringComparison.OrdinalIgnoreCase);

                if (isSource)
                {
                    var newQuantity = existing.DesiredQuantity - quantity;

                    if (newQuantity > 0)
                    {
                        result.Add(new DeckCardState
                        {
                            Card = existing.Card,
                            DesiredQuantity = newQuantity,
                            Section = existing.Section
                        });
                    }

                    continue;
                }

                if (isDestination)
                {
                    result.Add(new DeckCardState
                    {
                        Card = existing.Card,
                        DesiredQuantity = existing.DesiredQuantity + quantity,
                        Section = existing.Section
                    });

                    continue;
                }

                result.Add(existing);
            }

            if (destination is null)
            {
                result.Add(new DeckCardState
                {
                    Card = card,
                    DesiredQuantity = quantity,
                    Section = destinationSection
                });
            }

            return new DeckMutationResult
            {
                Succeeded = true,
                Cards = result
            };
        }

        // Commander rules
        public DeckSlotPlacementResult GetCommanderPlacement(DeckBuildingRuleContext context, OracleCard selectedCard)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(selectedCard);

            if (!CommanderFormats.IsCommanderLike(context.Format))
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
                return Allowed(DeckSlotPlacementAction.Add);
            }

            if (existingCommanders.Count == 1 && (AllowsAdditionalCommander(selectedCard) || AllowsAdditionalCommander(existingCommanders[0].Card)))
            {
                return Allowed(DeckSlotPlacementAction.Add);
            }

            return Allowed(DeckSlotPlacementAction.Replace);


            static DeckSlotPlacementResult Allowed(DeckSlotPlacementAction action)
            {
                return new DeckSlotPlacementResult
                {
                    Action = action
                };
            }

            static DeckSlotPlacementResult NotAllowed(string message)
            {
                return new DeckSlotPlacementResult
                {
                    Action = DeckSlotPlacementAction.NotAllowed,
                    Message = message
                };
            }
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
        public DeckSlotPlacementResult GetCompanionPlacement(DeckBuildingRuleContext context, OracleCard candidate)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(candidate);

            if (!CanBeCompanion(candidate))
            {
                return NotAllowed("The selected card is not eligible to be a companion.");
            }

            var currentCompanions = context.Entries.Where(entry => entry.Section == DeckSection.Companion).ToList();

            if (currentCompanions.Count == 0)
            {
                return Allowed(DeckSlotPlacementAction.Add);
            }

            if (currentCompanions.Any(entry => SameOracleCard(entry.Card, candidate)))
            {
                return NotAllowed("The selected card is already the companion.");
            }

            return Allowed(DeckSlotPlacementAction.Replace);
        }
        private static bool CanBeCompanion(OracleCard card)
        {
            return HasKeyword(card, "Companion");
        }
        private static DeckSlotPlacementResult Allowed(DeckSlotPlacementAction action)
        {
            return new DeckSlotPlacementResult
            {
                Action = action
            };
        }
        private static DeckSlotPlacementResult NotAllowed(string message)
        {
            return new DeckSlotPlacementResult
            {
                Action = DeckSlotPlacementAction.NotAllowed,
                Message = message
            };
        }
        private static bool SameOracleCard(OracleCard left, OracleCard right)
        {
            return string.Equals(left.ScryfallOracleId, right.ScryfallOracleId, StringComparison.OrdinalIgnoreCase);
        }

        // Deck stats
        public DeckStats CalculateDeckStats(IReadOnlyCollection<DeckCardState> cards)
        {
            // Include cards in the mainboard and commander sections and exlude cards that are not gameplay cards (e.g., tokens, emblems, etc.)
            var includedCards = cards.Where(card => card.Section is DeckSection.Mainboard or DeckSection.Commander && card.Card.GamePlayCard == 1).ToList();

            var cardCount = includedCards.Sum(card => card.DesiredQuantity);
            var landCount = includedCards.Where(card => GetCompositionType(card.Card) == DeckCompositionType.Land).Sum(card => card.DesiredQuantity);
            var creatureCount = includedCards.Where(card => GetCompositionType(card.Card) == DeckCompositionType.Creature).Sum(card => card.DesiredQuantity);
            var spellCount = includedCards.Where(card => GetCompositionType(card.Card) == DeckCompositionType.Other).Sum(card => card.DesiredQuantity);
            var nonLandCardCount = includedCards.Where(card => card.Card.Type?.Contains("Land") != true).Sum(card => card.DesiredQuantity);

            return new DeckStats
            {
                CardCount = cardCount,
                NonLandCardCount = nonLandCardCount,

                LandCount = landCount,
                LandPercentage = GetPercentage(landCount, cardCount),

                CreatureCount = creatureCount,
                CreaturePercentage = GetPercentage(creatureCount, cardCount),

                SpellCount = spellCount,
                SpellPercentage = GetPercentage(spellCount, cardCount),

                TypeBreakdown = CalculateTypeBreakdown(includedCards),
                ColorBreakdown = CalculateColorBreakdown(includedCards)
            };

            static double GetPercentage(int count, int total)
            {
                return total == 0 ? 0 : 100.0 * count / total;
            }
        }

        private static DeckCompositionType GetCompositionType(OracleCard card)
        {
            if (card.Type?.Contains("Land") == true)
            {
                return DeckCompositionType.Land;
            }

            if (card.Type?.Contains("Creature") == true)
            {
                return DeckCompositionType.Creature;
            }

            return DeckCompositionType.Other;
        }
        private enum DeckCompositionType
        {
            Land,
            Creature,
            Other
        }
        private static IReadOnlyList<DeckStatsBucket> CalculateTypeBreakdown(IReadOnlyCollection<DeckCardState> cards)
        {
            var types = new[]
            {
                "Creature",
                "Land",
                "Instant",
                "Sorcery",
                "Artifact",
                "Enchantment",
                "Planeswalker"
            };

            var buckets = types.Select(type => new DeckStatsBucket
            {
                Label = type,
                Count = cards.Where(card => card.Card.Type?.Contains(type) == true).Sum(card => card.DesiredQuantity)
            }).Where(bucket => bucket.Count > 0).ToList();

            var otherCount = cards.Where(card => !types.Any(type => card.Card.Type?.Contains(type) == true)).Sum(card => card.DesiredQuantity);

            if (otherCount > 0)
            {
                buckets.Add(new DeckStatsBucket
                {
                    Label = "Other",
                    Count = otherCount
                });
            }

            return buckets;
        }
        private static IReadOnlyList<DeckStatsBucket> CalculateColorBreakdown(IReadOnlyCollection<DeckCardState> cards)
        {
            var counts = new Dictionary<string, int>
            {
                ["W"] = 0,
                ["U"] = 0,
                ["B"] = 0,
                ["R"] = 0,
                ["G"] = 0,
                ["M"] = 0,
                ["C"] = 0
            };

            foreach (var card in cards)
            {
                if (card.Card.Type?.Contains("Land") == true)
                {
                    continue;
                }

                var colors = card.Card.Colors;

                string bucket;

                if (string.IsNullOrEmpty(colors))
                {
                    bucket = "C";
                }
                else if (colors.Length > 1)
                {
                    bucket = "M";
                }
                else
                {
                    bucket = colors;
                }

                counts[bucket] += card.DesiredQuantity;
            }

            return
            [
                .. counts.Where(x => x.Value > 0).Select(x => new DeckStatsBucket
                {
                    Label = x.Key,
                    Count = x.Value
                })
            ];
        }

        // Shared helpers
        private static bool HasKeyword(OracleCard card, string keyword)
        {
            return CsvValues.Contains(card.Keywords, keyword);
        }

    }
}
