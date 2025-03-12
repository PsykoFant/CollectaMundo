using CollectaMundo.Models;

namespace CollectaMundo.Tests
{
    class FilterTestUtilities
    {
        // Create a list of test cards
        public static List<CardSet> GetTestCards()
        {
            return
            [
                new CardSet { Name = "Black Lotus", Colors = "", ManaCost = "0", Rarity="rare" },
                new CardSet { Name = "Sol Ring", Colors = "", ManaCost = "1", Rarity="uncommon" },
                new CardSet { Name = "Lightning Bolt", Colors = "R", ManaCost = "R", Rarity="common" },
                new CardSet { Name = "Traben Inspector", Colors = "W", ManaCost = "W", Rarity="common" },
                new CardSet { Name = "Eldrazi Ravager", Colors = "", ManaCost = "5,C", Rarity="uncommon" },
                new CardSet { Name = "Island", Colors = "", ManaCost = "", Rarity="common" },
                new CardSet { Name = "Dromoka's Command", Colors = "G, W", ManaCost = "G,W", Rarity="bonus" },
                new CardSet { Name = "Biomass Mutation", Colors = "G, U", ManaCost = "X,G/U,G/U", Rarity="mythic" },
                new CardSet { Name = "Suffer The Past", Colors = "B", ManaCost = "X,B", Rarity="uncommon" },
                new CardSet { Name = "Kozilek's Command", Colors = "", ManaCost = "X,C,C", Rarity="common" },
            ];
        }
    }

    public class DummyFilterViewModel : FilterViewModel
    {
        public DummyFilterViewModel() : base(new CardViewModel()) { }
        public override void ApplyFiltering() { /* no-op */ }
        public override void DebugFullFilterState() { /* no-op */ }
    }
}
