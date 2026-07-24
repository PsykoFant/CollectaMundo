using CollectaMundo.ApplicationServices.Shared.UnitOfWork;
using CollectaMundo.DomainLogic.Decks;
using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.Infrastructure.Decks;

namespace CollectaMundo.ApplicationServices.Decks
{
    public sealed class DeckBuilderService(IUnitOfWorkRunner uowRunner, IDeckBuilderLogic deckBuilderLogic, IDeckBuilderRepo deckBuilderRepo) : IDeckBuilderService
    {
        private readonly IUnitOfWorkRunner _uowRunner = uowRunner;
        private readonly IDeckBuilderLogic _deckBuilderLogic = deckBuilderLogic;
        private readonly IDeckBuilderRepo _deckBuilderRepo = deckBuilderRepo;
        public Task<IReadOnlyList<DeckCardEntry>> LoadDeckAsync(int locationId)
        {
            return _uowRunner.ExecuteReadOnlyAsync(async conn =>
            {
                return await _deckBuilderRepo.GetByDeckLocationIdAsync(conn, locationId);
            });
        }
        public Task SaveDeckAsync(int locationId, IReadOnlyCollection<DeckCardState> cards)
        {
            return ReplaceDeckAsync(locationId, cards);
        }
        public Task SaveDeckAsync(int locationId, IEnumerable<DeckCardEntry> entries)
        {
            return _uowRunner.ExecuteWriteAsync(async (conn, tx) =>
            {
                await _deckBuilderRepo.ReplaceDeckAsync(conn, tx, locationId, [.. entries]);

                return (Result: true, Commit: true);
            });
        }
        public DeckActionAvailability GetActionAvailability(string? format, IReadOnlyCollection<DeckCardState> deckCards, OracleCard selectedCard)
        {
            var context = CreateRuleContext(format, deckCards);

            return _deckBuilderLogic.GetActionAvailability(
                context,
                selectedCard);
        }
        private static DeckBuildingRuleContext CreateRuleContext(string? format, IEnumerable<DeckCardState> cards)
        {
            return new DeckBuildingRuleContext
            {
                Format = format,
                Entries = [.. cards.Select(x => new DeckBuildingRuleEntry { Card = x.Card, Section = x.Section })]
            };
        }
        public async Task<SetCommanderResult> SetCommanderAsync(int deckLocationId, string? format, IReadOnlyCollection<DeckCardState> currentCards, OracleCard selectedCard)
        {
            ArgumentNullException.ThrowIfNull(currentCards);
            ArgumentNullException.ThrowIfNull(selectedCard);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(deckLocationId);

            var context = CreateRuleContext(format, currentCards);
            var placement = _deckBuilderLogic.GetCommanderPlacement(context, selectedCard);

            if (!placement.IsAllowed)
            {
                return new SetCommanderResult
                {
                    Succeeded = false,
                    Message = placement.Message,
                    Cards = [.. currentCards]
                };
            }

            var updatedCards = ApplyCommanderPlacement(currentCards, selectedCard, placement.Action);

            await ReplaceDeckAsync(deckLocationId, updatedCards);

            return new SetCommanderResult
            {
                Succeeded = true,
                Cards = updatedCards
            };
        }

        // Helpers
        private Task ReplaceDeckAsync(int deckLocationId, IReadOnlyCollection<DeckCardState> cards)
        {
            var entries = MapToDeckCardEntries(deckLocationId, cards);

            return _uowRunner.ExecuteWriteAsync(async (connection, transaction) =>
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
        private static IReadOnlyList<DeckCardState> ApplyCommanderPlacement(IReadOnlyCollection<DeckCardState> currentCards, OracleCard selectedCard, CommanderPlacementAction action)
        {
            if (action == CommanderPlacementAction.NotAllowed)
            {
                throw new InvalidOperationException("A disallowed commander placement cannot be applied.");
            }

            var updatedCards = currentCards.ToList();

            if (action == CommanderPlacementAction.Replace)
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
    }
}
