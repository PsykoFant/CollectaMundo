using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.DomainLogic.Decks.Models.Enums;
using CollectaMundo.DomainLogic.Shared.CollectionSnapshot;

namespace CollectaMundo.DomainLogic.Decks
{
    public sealed class DeckExportLogic : IDeckExportLogic
    {
        public IReadOnlyList<DeckCardState> GetCompleteDeck(IEnumerable<DeckCardState> cards)
        {
            return [.. cards.Where(card => IsExportableSection(card.Section) && card.DesiredQuantity > 0).Select(card => new DeckCardState
            {
                Card = card.Card,
                Section = card.Section,
                DesiredQuantity = card.DesiredQuantity
            })];
        }
        public IReadOnlyList<MissingDeckCard> GetMissingCards(IEnumerable<DeckCardState> cards, ICollectionQuantitySnapshot quantitySnapshot, int deckLocationId)
        {
            return [.. cards.Where(card => IsExportableSection(card.Section) && card.DesiredQuantity > 0).GroupBy(card => card.Card.ScryfallOracleId).Select(group =>
            {
                var card = group.First().Card;
                var desiredQuantity = group.Sum(x => x.DesiredQuantity);
                var availableQuantity = quantitySnapshot.GetAvailableQuantity(card.ScryfallOracleId,deckLocationId);
                var missingQuantity =Math.Max(desiredQuantity - availableQuantity, 0);

                return new MissingDeckCard
                {
                    Card = card,
                    DesiredQuantity = desiredQuantity,
                    AvailableQuantity = availableQuantity,
                    MissingQuantity = missingQuantity
                };
            }).Where(card => card.MissingQuantity > 0)];
        }
        private static bool IsExportableSection(DeckSection section)
        {
            return section is DeckSection.Mainboard or DeckSection.Sideboard or DeckSection.Commander or DeckSection.Companion;
        }
    }
}
