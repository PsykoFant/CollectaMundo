using CollectaMundo.ApplicationServices.Decks.Shared;
using CollectaMundo.DomainLogic.Decks;
using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.DomainLogic.Shared.CollectionSnapshot;

namespace CollectaMundo.ApplicationServices.Decks.DeckExport
{
    public sealed class DeckExportService(IDeckCardReader deckCardReader, IDeckExportLogic deckExportLogic) : IDeckExportService
    {
        private readonly IDeckCardReader _deckCardReader = deckCardReader;
        private readonly IDeckExportLogic _deckExportLogic = deckExportLogic;

        public async Task<IReadOnlyList<DeckCardState>> GetCompleteDeckAsync(int deckLocationId, IReadOnlyList<OracleCard> oracleCards)
        {
            var entries = await _deckCardReader.LoadAsync(deckLocationId);
            var states = DeckCardStateMapper.CreateStates(entries, oracleCards);

            return _deckExportLogic.GetCompleteDeck(states);
        }
        public async Task<IReadOnlyList<MissingDeckCard>> GetMissingCardsAsync(int deckLocationId, IReadOnlyList<OracleCard> oracleCards, ICollectionQuantitySnapshot quantitySnapshot)
        {
            var entries = await _deckCardReader.LoadAsync(deckLocationId);
            var states = DeckCardStateMapper.CreateStates(entries, oracleCards);

            return _deckExportLogic.GetMissingCards(
                states,
                quantitySnapshot,
                deckLocationId);
        }
        public string Format(IReadOnlyList<MissingDeckCard> cards)
        {
            ArgumentNullException.ThrowIfNull(cards);

            return string.Join(Environment.NewLine, cards.OrderBy(card => card.Card.Name, StringComparer.OrdinalIgnoreCase).Select(card => $"{card.MissingQuantity} {card.Card.Name}"));
        }
    }
}

