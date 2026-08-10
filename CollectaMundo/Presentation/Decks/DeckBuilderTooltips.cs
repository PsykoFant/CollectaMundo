using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CollectaMundo.Presentation.Decks
{
    public static class DeckBuilderTooltips
    {
        public const string Owned = "Cards you own in your collection.";

        public const string Available = "Available to this deck from your collection (owned minus copies allocated to other decks).";

        public const string Allocated = "Copies from your collection allocated to this deck.";

        public const string DesiredQuantity = "Cards in this deck.";

        public const string IllegalCard = "This card is not legal in the selected format.";

        public const string InsufficientAvailableQuantity = "Desired quantity exceeds the number of copies available to this deck.";
    }
}
