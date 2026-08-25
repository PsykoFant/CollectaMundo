using CollectaMundo.ApplicationServices.CardLegalities;
using CollectaMundo.ApplicationServices.Decks.Models;
using CollectaMundo.ApplicationServices.Shared.UnitOfWork;
using CollectaMundo.DomainLogic.Decks;
using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.DomainLogic.Decks.Models.Enums;
using CollectaMundo.DomainLogic.Decks.Models.Records;
using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.Infrastructure.Decks;
using CollectaMundo.ViewModels.Decks.Models.DragMoveViewRequests;

namespace CollectaMundo.ApplicationServices.Decks
{
    public sealed class DeckBuilderService(IUnitOfWorkRunner uowRunner, ICardLegalityProviderService cardLegalityProviderService, IDeckBuilderLogic deckBuilderLogic, IDeckBuilderRepo deckBuilderRepo) : IDeckBuilderService
    {
        private readonly IUnitOfWorkRunner _uowRunner = uowRunner;
        private readonly ICardLegalityProviderService _cardLegalityProviderService = cardLegalityProviderService;
        private readonly IDeckBuilderLogic _deckBuilderLogic = deckBuilderLogic;
        private readonly IDeckBuilderRepo _deckBuilderRepo = deckBuilderRepo;
        public Task<IReadOnlyList<DeckCardEntry>> LoadDeckAsync(int locationId)
        {
            return _uowRunner.ExecuteReadOnlyAsync(async conn =>
            {
                return await _deckBuilderRepo.GetByDeckLocationIdAsync(conn, locationId);
            });
        }
        public async Task<DeckMutationResult> AddCardsAsync(int deckLocationId, IReadOnlyCollection<DeckCardState> currentCards, IReadOnlyCollection<OracleCard> selectedCards, int quantity, DeckSection section)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(deckLocationId);
            ArgumentNullException.ThrowIfNull(currentCards);
            ArgumentNullException.ThrowIfNull(selectedCards);

            if (quantity <= 0)
            {
                return Failure(currentCards, "The quantity to add must be greater than zero.");
            }

            if (selectedCards.Count == 0)
            {
                return Failure(currentCards, "No cards were selected.");
            }

            if (section is DeckSection.Commander
                or DeckSection.Companion)
            {
                return Failure(currentCards, "Commander and companion cards must use their dedicated operations.");
            }

            var updatedCards = currentCards.ToList();

            foreach (var selectedCard in selectedCards)
            {
                var existingIndex = updatedCards.FindIndex(x => x.Section == section && string.Equals(x.Card.ScryfallOracleId, selectedCard.ScryfallOracleId, StringComparison.OrdinalIgnoreCase));

                if (existingIndex >= 0)
                {
                    var existing = updatedCards[existingIndex];

                    updatedCards[existingIndex] = new DeckCardState
                    {
                        Card = existing.Card,
                        DesiredQuantity = existing.DesiredQuantity + quantity,
                        Section = existing.Section
                    };

                    continue;
                }

                updatedCards.Add(new DeckCardState
                {
                    Card = selectedCard,
                    DesiredQuantity = quantity,
                    Section = section
                });
            }

            await PersistDeckStateAsync(deckLocationId, updatedCards);
            return Success(updatedCards);
        }
        public async Task<DeckMutationResult> DeleteCardsAsync(int deckLocationId, IReadOnlyCollection<DeckCardState> currentCards, IReadOnlyCollection<DeckCardIdentityRecord> cardsToDelete)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(deckLocationId);
            ArgumentNullException.ThrowIfNull(currentCards);
            ArgumentNullException.ThrowIfNull(cardsToDelete);

            if (cardsToDelete.Count == 0)
            {
                return Failure(currentCards, "No deck cards were selected for deletion.");
            }

            var deleteKeys = cardsToDelete.Select(x => new DeckCardKey(x.OracleId, x.Section)).ToHashSet();
            var updatedCards = currentCards.Where(card => !cardsToDelete.Any(target => target.Section == card.Section && string.Equals(target.OracleId, card.Card.ScryfallOracleId, StringComparison.OrdinalIgnoreCase))).ToList();

            if (updatedCards.Count == currentCards.Count)
            {
                return Failure(currentCards, "None of the selected deck cards were found.");
            }

            await PersistDeckStateAsync(deckLocationId, updatedCards);
            return Success(updatedCards);
        }
        public async Task<DeckMutationResult> RemoveCardQuantitiesAsync(int deckLocationId, IReadOnlyCollection<DeckCardState> currentCards, IReadOnlyCollection<DeckCardQuantityRemoval> removals)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(deckLocationId);
            ArgumentNullException.ThrowIfNull(currentCards);
            ArgumentNullException.ThrowIfNull(removals);

            if (removals.Count == 0)
            {
                return Failure(currentCards, "No deck cards were selected for removal.");
            }

            var updatedCards = currentCards.ToList();

            foreach (var removal in removals)
            {
                if (removal.Quantity <= 0)
                {
                    return Failure(currentCards, "The quantity to remove must be greater than zero.");
                }

                if (removal.Section is DeckSection.Commander or DeckSection.Companion)
                {
                    return Failure(currentCards, "Commander and companion quantities cannot be edited.");
                }

                var index = updatedCards.FindIndex(card => card.Section == removal.Section && string.Equals(card.Card.ScryfallOracleId, removal.Card.ScryfallOracleId, StringComparison.OrdinalIgnoreCase));

                if (index < 0)
                {
                    return Failure(currentCards, $"The selected deck card '{removal.Card.Name}' could not be found.");
                }

                var existing = updatedCards[index];
                var desiredQuantity = existing.DesiredQuantity - removal.Quantity;

                if (desiredQuantity <= 0)
                {
                    updatedCards.RemoveAt(index);
                }
                else
                {
                    updatedCards[index] = new DeckCardState
                    {
                        Card = existing.Card,
                        DesiredQuantity = desiredQuantity,
                        Section = existing.Section
                    };
                }
            }

            await PersistDeckStateAsync(deckLocationId, updatedCards);

            return Success(updatedCards);
        }
        public async Task<DeckMutationResult> SetCardQuantityAsync(int deckLocationId, IReadOnlyCollection<DeckCardState> currentCards, DeckCardIdentityRecord card, int desiredQuantity)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(deckLocationId);
            ArgumentNullException.ThrowIfNull(currentCards);
            ArgumentNullException.ThrowIfNull(card);

            if (card.Section is DeckSection.Commander or DeckSection.Companion)
            {
                return Failure(currentCards, "Commander and companion quantities cannot be edited.");
            }

            var updatedCards = currentCards.ToList();

            var index = updatedCards.FindIndex(x => Matches(x, card));

            if (index < 0)
            {
                return Failure(currentCards, "The selected deck card could not be found.");
            }

            if (desiredQuantity <= 0)
            {
                updatedCards.RemoveAt(index);
            }
            else
            {
                var existing = updatedCards[index];

                updatedCards[index] = new DeckCardState
                {
                    Card = existing.Card,
                    DesiredQuantity = desiredQuantity,
                    Section = existing.Section
                };
            }

            await PersistDeckStateAsync(deckLocationId, updatedCards);
            return Success(updatedCards);
        }
        public async Task<DeckMutationResult> MoveCardsAsync(int deckLocationId, IReadOnlyCollection<DeckCardState> currentCards, IReadOnlyCollection<DeckCardMoveRequest> moves, DeckSection destinationSection)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(deckLocationId);
            ArgumentNullException.ThrowIfNull(currentCards);
            ArgumentNullException.ThrowIfNull(moves);

            if (moves.Count == 0)
            {
                return Failure(currentCards, "No deck cards were selected for moving.");
            }

            IReadOnlyCollection<DeckCardState> updatedCards = currentCards;

            foreach (var move in moves)
            {
                var result = _deckBuilderLogic.MoveCard(updatedCards, move.Card, move.SourceSection, destinationSection, move.Quantity);

                if (!result.Succeeded)
                {
                    return Failure(currentCards, result.Message ?? $"Failed to move '{move.Card.Name}'.");
                }

                updatedCards = result.Cards;
            }

            await PersistDeckStateAsync(deckLocationId, updatedCards);

            return Success([.. updatedCards]);
        }
        private static bool Matches(DeckCardState state, DeckCardIdentityRecord identity)
        {
            return state.Section == identity.Section && string.Equals(state.Card.ScryfallOracleId, identity.OracleId, StringComparison.OrdinalIgnoreCase);
        }
        public DeckActionAvailability GetActionAvailability(string? format, IReadOnlyCollection<DeckCardState> deckCards, OracleCard selectedCard)
        {
            var context = CreateRuleContext(format, deckCards);

            return _deckBuilderLogic.GetActionAvailability(context, selectedCard);
        }
        public DeckCardValidationResult ValidateCard(string? format, IReadOnlyCollection<DeckCardState> deckCards, DeckCardEntry entry, OracleCard oracleCard)
        {
            ArgumentNullException.ThrowIfNull(deckCards);
            ArgumentNullException.ThrowIfNull(entry);
            ArgumentNullException.ThrowIfNull(oracleCard);

            var context = CreateRuleContext(format, deckCards);

            var formatInfo = _cardLegalityProviderService.GetFormat(format);

            return _deckBuilderLogic.ValidateCard(context, entry, oracleCard, formatInfo?.Mask);
        }
        private static DeckBuildingRuleContext CreateRuleContext(string? format, IEnumerable<DeckCardState> cards)
        {
            return new DeckBuildingRuleContext
            {
                Format = format,
                Entries = [.. cards.Select(x => new DeckBuildingRuleEntry { Card = x.Card, Section = x.Section })]
            };
        }
        public async Task<DeckMutationResult> SetCommanderAsync(int deckLocationId, string? format, IReadOnlyCollection<DeckCardState> currentCards, OracleCard selectedCard)
        {
            ArgumentNullException.ThrowIfNull(currentCards);
            ArgumentNullException.ThrowIfNull(selectedCard);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(deckLocationId);

            var context = CreateRuleContext(format, currentCards);
            var placement = _deckBuilderLogic.GetCommanderPlacement(context, selectedCard);

            if (!placement.IsAllowed)
            {
                return new DeckMutationResult
                {
                    Succeeded = false,
                    Message = placement.Message,
                    Cards = [.. currentCards]
                };
            }

            var updatedCards = ApplyCommanderPlacement(currentCards, selectedCard, placement.Action);

            await PersistDeckStateAsync(deckLocationId, updatedCards);

            return new DeckMutationResult
            {
                Succeeded = true,
                Cards = updatedCards
            };
        }
        public async Task<DeckMutationResult> SetCompanionAsync(int deckLocationId, string? format, IReadOnlyCollection<DeckCardState> currentCards, OracleCard selectedCard)
        {
            var context = CreateRuleContext(format, currentCards);

            var placement = _deckBuilderLogic.GetCompanionPlacement(context, selectedCard);

            if (!placement.IsAllowed)
            {
                return Failure(currentCards, placement.Message ?? "The selected card cannot be placed in that deck slot.");
            }

            var updatedCards = currentCards.Where(card => card.Section != DeckSection.Companion).ToList();

            updatedCards.Add(new DeckCardState
            {
                Card = selectedCard,
                DesiredQuantity = 1,
                Section = DeckSection.Companion
            });

            await PersistDeckStateAsync(deckLocationId, updatedCards);

            return Success(updatedCards);
        }

        // Helpers
        private static DeckMutationResult Success(IReadOnlyList<DeckCardState> cards)
        {
            return new DeckMutationResult
            {
                Succeeded = true,
                Cards = cards
            };
        }
        private static DeckMutationResult Failure(IReadOnlyCollection<DeckCardState> currentCards, string message)
        {
            return new DeckMutationResult
            {
                Succeeded = false,
                Message = message,
                Cards = [.. currentCards]
            };
        }
        private async Task PersistDeckStateAsync(int deckLocationId, IReadOnlyCollection<DeckCardState> cards)
        {
            var entries = MapToDeckCardEntries(deckLocationId, cards);

            await _uowRunner.ExecuteWriteAsync(async (connection, transaction) =>
                {
                    await _deckBuilderRepo.ReplaceDeckAsync(connection, transaction, deckLocationId, entries);
                    return (Result: true, Commit: true);
                });
        }
        private static IReadOnlyList<DeckCardEntry> MapToDeckCardEntries(int deckLocationId, IEnumerable<DeckCardState> cards)
        {
            return
            [
                .. cards.Select(x => new DeckCardEntry
                {
                    DeckLocationId = deckLocationId,
                    OracleId = x.Card.ScryfallOracleId,
                    CardName = x.Card.Name,
                    DesiredQuantity = x.DesiredQuantity,
                    Section = x.Section
                })];
        }
        private static List<DeckCardState> ApplyCommanderPlacement(IReadOnlyCollection<DeckCardState> currentCards, OracleCard selectedCard, DeckSlotPlacementAction action)
        {
            if (action == DeckSlotPlacementAction.NotAllowed)
            {
                throw new InvalidOperationException("A disallowed commander placement cannot be applied.");
            }

            var updatedCards = currentCards.ToList();

            if (action == DeckSlotPlacementAction.Replace)
            {
                updatedCards.RemoveAll(x => x.Section == DeckSection.Commander);
            }

            updatedCards.Add(new DeckCardState
            {
                Card = selectedCard,
                DesiredQuantity = 1,
                Section = DeckSection.Commander
            });

            return updatedCards;
        }
        private sealed record DeckCardKey(string OracleId, DeckSection Section);
    }

}
