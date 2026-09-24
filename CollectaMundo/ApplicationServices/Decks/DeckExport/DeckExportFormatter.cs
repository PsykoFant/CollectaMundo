using CollectaMundo.DomainLogic.Decks.Models;

namespace CollectaMundo.ApplicationServices.Decks.DeckExport
{
    public static class DeckExportFormatter
    {
        public static string FormatCardmarketWantList(IReadOnlyList<MissingDeckCard> cards)
        {
            ArgumentNullException.ThrowIfNull(cards);

            return string.Join(Environment.NewLine, cards.OrderBy(card => card.Card.Name, StringComparer.OrdinalIgnoreCase).Select(card => $"{card.MissingQuantity} {card.Card.Name}"));
        }
    }
}
