using CollectaMundo.DomainLogic.Decks.Models;
using System.Globalization;

namespace CollectaMundo.ApplicationServices.Decks.DeckExport
{
    public static class DeckExportFormatter
    {
        public static string FormatCardmarketWantList(IReadOnlyList<MissingDeckCard> cards)
        {
            ArgumentNullException.ThrowIfNull(cards);

            return string.Join(Environment.NewLine, cards.OrderBy(card => card.Card.Name, StringComparer.OrdinalIgnoreCase).Select(card => $"{card.MissingQuantity} {card.Card.Name}"));
        }
        public static IReadOnlyList<string> GetCompleteDeckCsvHeaders()
        {
            return
            [
                "Quantity",
                "Name",
                "Section",
                "Type",
                "Mana Cost",
                "Scryfall Oracle ID"
            ];
        }
        public static IEnumerable<IReadOnlyList<string?>> CreateCompleteDeckCsvRows(IReadOnlyList<DeckCardState> cards)
        {
            ArgumentNullException.ThrowIfNull(cards);

            foreach (var card in cards)
            {
                yield return new string?[]
                {
                    card.DesiredQuantity.ToString(CultureInfo.InvariantCulture),
                    card.Card.Name,
                    card.Section.ToString(),
                    card.Card.Type,
                    card.Card.ManaCost,
                    card.Card.ScryfallOracleId
                };
            }
        }
    }
}
