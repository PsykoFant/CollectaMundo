using CollectaMundo.Models;

namespace CollectaMundo.Tests
{
    class FilterTestUtilities
    {
        public static List<CardSet> GetTestCards()
        {
            return new List<CardSet>
            {
                new CardSet
                {
                    Name = "Davros, Dalek Creator",SetName = "Doctor Who",ManaCost = "1,U,B,R",Types = "Artifact, Creature",
                    Colors = "B,R,U",
                    SuperTypes = "Legendary",SubTypes = "Alien, Scientist",Type = "Legendary Artifact Creature — Alien Scientist",Keywords = "Menace",
                    Text = "Menace\nAt the beginning of your end step, create a 3/3 black Dalek artifact creature token with menace if an opponent lost 3 or more life this turn. Then each opponent who lost 3 or more life this turn faces a villainous choice — You draw a card, or that player discards a card.",
                    ManaValue = 4,Language = "English",Finishes = "nonfoil, foil",Rarity = "mythic",CardsOwned=0,CardsForTrade=0, SelectedCondition=null, SelectedFinish=null
                },
                new CardSet
                {
                    Name = "Skeletal Swarming",SetName = "Magic Online Promos",ManaCost = "3,B,G",Types = "Enchantment",
                    Colors = "B,G",
                    SuperTypes = "",SubTypes = "",Type = "Enchantment",
                    Keywords = "",
                    Text = "Each Skeleton you control has trample, attacks each combat if able, and gets +X/+0, where X is the number of other Skeletons you control.\nAt the beginning of your end step, create a tapped 1/1 black Skeleton creature token. If a creature died this turn, create two of those tokens instead.",
                    ManaValue = 5,Language = "English",Finishes = "nonfoil, foil",Rarity = "rare",CardsOwned=0,CardsForTrade=0, SelectedCondition=null, SelectedFinish=null
                },
                new CardSet
                {
                    Name = "Lumbering Laundry",SetName = "Murders at Karlov Manor",ManaCost = "5",Types = "Artifact, Creature",
                    Colors = "",
                    SuperTypes = "",SubTypes = "Golem",Type = "Artifact Creature — Golem",Keywords = "Disguise",
                    Text = "{2}: Until end of turn, you may look at face-down creatures you don't control any time.\nDisguise {5} (You may cast this card face down for {3} as a 2/2 creature with ward {2}. Turn it face up any time for its disguise cost.)",
                    ManaValue = 5,Language = "English",Finishes = "nonfoil, foil",Rarity = "uncommon",CardsOwned=0,CardsForTrade=0, SelectedCondition=null, SelectedFinish=null
                },
                new CardSet
                {
                    Name = "Olivia Voldaren",SetName = "Innistrad Remastered",ManaCost = "2,B,R",Types = "Creature",
                    Colors = "B,R",
                    SuperTypes = "Legendary",SubTypes = "Vampire",Type = "Legendary Creature — Vampire",Keywords = "Flying",
                    Text = "Flying\n{1}{R}: Olivia Voldaren deals 1 damage to another target creature. That creature becomes a Vampire in addition to its other types. Put a +1/+1 counter on Olivia Voldaren.\n{3}{B}{B}: Gain control of target Vampire for as long as you control Olivia Voldaren.",
                    ManaValue = 4,Language = "English",Finishes = "nonfoil, foil",Rarity = "mythic",CardsOwned=0,CardsForTrade=0, SelectedCondition=null, SelectedFinish=null
                },
                new CardSet
                {
                    Name = "Rock Hydra",SetName = "30th Anniversary Edition",ManaCost = "X,R,R",Types = "Creature",
                    Colors = "R",
                    SuperTypes = "",SubTypes = "Hydra",Type = "Creature — Hydra",Keywords = "",
                    Text = "This creature enters with X +1/+1 counters on it.\nFor each 1 damage that would be dealt to this creature, if it has a +1/+1 counter on it, remove a +1/+1 counter from it and prevent that 1 damage.\n{R}: Prevent the next 1 damage that would be dealt to this creature this turn.\n{R}{R}{R}: Put a +1/+1 counter on this creature. Activate only during your upkeep.",
                    ManaValue = 2,Language = "English",Finishes = "nonfoil",Rarity = "rare",CardsOwned=0,CardsForTrade=0, SelectedCondition=null, SelectedFinish=null
                },
                new CardSet
                {
                    Name = "Time Walk",SetName = "30th Anniversary Edition",ManaCost = "1,U",Types = "Sorcery",
                    Colors = "U",
                    SuperTypes = "",SubTypes = "",Type = "Sorcery",Keywords = "",
                    Text = "Take an extra turn after this one.",
                    ManaValue = 2,Language = "English",Finishes = "nonfoil",Rarity = "rare",CardsOwned=1,CardsForTrade=0, SelectedCondition="Poor", SelectedFinish="nonfoil"
                },
                new CardSet
                {
                    Name = "Struggle // Survive",SetName = "March of the Machine Commander",ManaCost = "2,R",Types = "Instant",
                    Colors = "R",
                    SuperTypes = "",SubTypes = "",Type = "Instant",Keywords = "Aftermath",
                    Text = "Struggle deals damage to target creature equal to the number of lands you control.",
                    ManaValue = 5,Language = "English",Finishes = "nonfoil",Rarity = "uncommon",CardsOwned=3,CardsForTrade=1, SelectedCondition="Near Mint", SelectedFinish="nonfoil"
                },
                new CardSet
                {
                    Name = "Lovestruck Beast // Heart's Desire",SetName = "Commander Legends: Battle for Baldur's Gate",ManaCost = "2,G",Types = "Creature",
                    Colors = "G",
                    SuperTypes = "",SubTypes = "Beast, Noble",Type = "Creature — Beast Noble",Keywords = "",
                    Text = "This creature can't attack unless you control a 1/1 creature.",
                    ManaValue = 3,Language = "English",Finishes = "nonfoil",Rarity = "rare",CardsOwned=0,CardsForTrade=0, SelectedCondition=null, SelectedFinish=null
                },
                new CardSet
                {
                    Name = "Garruk Relentless // Garruk, the Veil-Cursed",SetName = "Innistrad Remastered",ManaCost = "3,G",Types = "Planeswalker",
                    Colors = "G,B",
                    SuperTypes = "Legendary",SubTypes = "Garruk",Type = "Legendary Planeswalker — Garruk",Keywords = "Transform",
                    Text = "When Garruk has two or fewer loyalty counters on him, transform him.\n[0]: Garruk deals 3 damage to target creature. That creature deals damage equal to its power to him.\n[0]: Create a 2/2 green Wolf creature token.",
                    ManaValue = 4,Language = "English",Finishes = "nonfoil, foil",Rarity = "mythic",CardsOwned=0,CardsForTrade=0, SelectedCondition=null, SelectedFinish=null
                },
                new CardSet
                {
                    Name = "Kozilek's Command",SetName = "Modern Horizons 3",ManaCost = "X,C,C",Types = "Kindred, Instant",
                    Colors = "",
                    SuperTypes = "",SubTypes = "Eldrazi",Type = "Kindred Instant — Eldrazi",Keywords = "",
                    Text = "Choose two —\n• Target player creates X 0/1 colorless Eldrazi Spawn creature tokens with \"Sacrifice this creature: Add {C}.\"\n• Target player scries X, then draws a card.\n• Exile target creature with mana value X or less.\n• Exile up to X target cards from graveyards.",
                    ManaValue = 2,Language = "English",Finishes = "nonfoil, foil",Rarity = "rare",CardsOwned=15,CardsForTrade=14, SelectedCondition="Excellent", SelectedFinish="foil"
                },
                new CardSet
                {
                    Name = "Propagator Drone",SetName = "Modern Horizons 3",ManaCost = "1,G",Types = "Creature",
                    Colors = "",
                    SuperTypes = "",SubTypes = "Eldrazi, Drone",Type = "Creature — Eldrazi Drone",Keywords = "Devoid",
                    Text = "Devoid (This card has no color.)\nCreature tokens you control have evolve. (They have \"Whenever a creature you control enters, if it has greater power or toughness than this token, put a +1/+1 counter on this token.\" They see this creature enter.)\n{3}{G}: Create a 0/1 colorless Eldrazi Spawn creature token with \"Sacrifice this token: Add {C}.\"",
                    ManaValue = 2,Language = "English",Finishes = "nonfoil, foil",Rarity = "uncommon",CardsOwned=0,CardsForTrade=0, SelectedCondition=null, SelectedFinish=null
                },
                new CardSet
                {
                    Name = "Fire // Ice",SetName = "Invasion",ManaCost = "1,R",Types = "Instant",
                    Colors = "R,U",
                    SuperTypes = "",SubTypes = "",Type = "Instant",Keywords = "",
                    Text = "Fire deals 2 damage divided as you choose among one or two targets.",
                    ManaValue = 4,Language = "English",Finishes = "nonfoil, foil",Rarity = "uncommon",CardsOwned=0,CardsForTrade=0, SelectedCondition=null, SelectedFinish=null
                },
                new CardSet
                {
                    Name = "Tarfire",SetName = "The List",ManaCost = "R",Types = "Kindred, Instant",
                    Colors = "R",
                    SuperTypes = "",SubTypes = "Goblin",Type = "Kindred Instant — Goblin",Keywords = "",
                    Text = "Tarfire deals 2 damage to any target.",
                    ManaValue = 1,Language = "English",Finishes = "nonfoil",Rarity = "common",CardsOwned=0,CardsForTrade=0, SelectedCondition=null, SelectedFinish=null
                },
                new CardSet
                {
                    Name = "Begin the Invasion",SetName = "March of the Machine Commander",ManaCost = "X,W,U,B,R,G",Types = "Sorcery",
                    Colors = "B,G,R,U,W",
                    SuperTypes = "",SubTypes = "",Type = "Sorcery",Keywords = "",
                    Text = "Search your library for up to X battle cards with different names, put them onto the battlefield, then shuffle.",
                    ManaValue = 5,Language = "English",Finishes = "nonfoil, foil",Rarity = "mythic",CardsOwned=2,CardsForTrade=0, SelectedCondition="Mint", SelectedFinish="foil"
                },
                new CardSet
                {
                    Name = "Lukka, Bound to Ruin",SetName = "Phyrexia: All Will Be One",ManaCost = "2,R,R/G/P,G",Types = "Planeswalker",
                    Colors = "G,R",
                    SuperTypes = "Legendary",SubTypes = "Lukka",Type = "Legendary Planeswalker — Lukka",Keywords = "Compleated",
                    Text = "Compleated ({R/G/P} can be paid with {R}, {G}, or 2 life. If life was paid, this planeswalker enters with two fewer loyalty counters.)\n[+1]: Add {R}{G}. Spend this mana only to cast creature spells or activate abilities of creatures.\n[−1]: Create a 3/3 green Phyrexian Beast creature token with toxic 1.\n[−4]: Lukka deals X damage divided as you choose among any number of target creatures and/or planeswalkers, where X is the greatest power among creatures you control as you activate this ability.",
                    ManaValue = 5,Language = "English",Finishes = "nonfoil, foil",Rarity = "mythic",CardsOwned=0,CardsForTrade=0, SelectedCondition=null, SelectedFinish=null
                },
                new CardSet
                {
                    Name = "Cat",SetName = "Aetherdrift",ManaCost = "",Types = "Token, Creature",
                    Colors = "W",
                    SuperTypes = "",SubTypes = "Cat",Type = "Token Creature — Cat",Keywords = "Lifelink",
                    Text = "Lifelink (Damage dealt by this creature also causes you to gain that much life.)",
                    ManaValue = 0,Language = "English",Finishes = "nonfoil, foil",Rarity = "",CardsOwned=3,CardsForTrade=3, SelectedCondition="Near Mint", SelectedFinish="nonfoil"
                },
                new CardSet
                {
                    Name = "Bounty: Eriana, Wrecking Ball // Wanted!",SetName = "Outlaws of Thunder Junction Commander",ManaCost = "",Types = "Card",
                    Colors = "",
                    SuperTypes = "",SubTypes = "",Type = "Card",Keywords = "",
                    Text = "At the beginning of your end step, if you committed a crime this turn, collect your reward. (Targeting opponents, anything they control, and/or cards in their graveyards is a crime.)",
                    ManaValue = 0,Language = "English",Finishes = "nonfoil, foil",Rarity = "",CardsOwned=0,CardsForTrade=0, SelectedCondition=null, SelectedFinish=null
                },
                new CardSet
                {
                    Name = "Tundra",SetName = "30th Anniversary Edition",ManaCost = "",Types = "Land",
                    Colors = "",
                    SuperTypes = "",SubTypes = "Plains,Island",Type = "Land — Plains Island",Keywords = "",
                    Text = "{T}: Add {W} or {U}.",
                    ManaValue = 0,Language = "English",Finishes = "nonfoil",Rarity = "rare",CardsOwned=0,CardsForTrade=0, SelectedCondition=null, SelectedFinish=null
                }
            };
        }

        public static FilterItemViewModel CreateNumericFilter()
        {
            // Use a dummy FilterViewModel to avoid UI side‐effects.
            var dummyFvm = new DummyFilterViewModel();

            // Get the test cards.
            var testCards = GetTestCards();
            // Extract unique numeric values from the "ManaValue" field.
            // (Assuming that ManaValue is effectively an integer value; if not, you might want to adjust accordingly.)
            var numericOptions = testCards
                .Select(static card => (int)card.ManaValue)
                .Distinct()
                .OrderBy(static x => x)
                .ToList();


            // For numeric filters you typically don't have pre–defined multi–select options,
            // so we can pass an empty list for the FilterOption collection.
            var emptyOptions = new List<FilterOption>();

            // Create the filter item view model.
            // "ManaValue" is used as the criteria key.
            // The default text is "ManaValue ..." (or "Mana Value ..." for readability),
            // and we pass the numericOptions as the list of available numeric values.
            return new FilterItemViewModel("ManaValue", emptyOptions, "ManaValue ...", "Mana Value", dummyFvm, numericOptions);
        }


        public static FilterItemViewModel CreateNameFilter()
        {
            // Use the dummy view model to avoid UI side effects.
            var dummyFvm = new DummyFilterViewModel();

            // Get distinct names from the test cards.
            var testCards = GetTestCards();
            var distinctNames = testCards
                .Select(static card => card.Name)
                .Where(static name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Create FilterOption objects for each distinct name.
            var options = distinctNames.Select(static name => new FilterOption(name)).ToList();

            // "Name" is a single-selection (free text) filter.
            // Set the default text to "Name ..." and use "Name" as the readable label.
            return new FilterItemViewModel("Name", options, "Name ...", "Name", dummyFvm);
        }

        public static FilterItemViewModel CreateColorFilter()
        {
            var dummyFvm = new DummyFilterViewModel();

            // Define filter options for colors.
            var options = new List<FilterOption>
            {
                new FilterOption("R"),
                new FilterOption("W"),
                new FilterOption("G"),
                new FilterOption("U"),
                new FilterOption("B"),
                new FilterOption("C"),
                new FilterOption("X"),
                new FilterOption("Colorless")
            };

            // Construct the FilterItemViewModel for "Colors". 
            // Here, "Colors ..." is used as DefaultText and ReadableLabel.
            return new FilterItemViewModel("Colors", options, "Colors ...", "Colors", dummyFvm);
        }
        public static FilterItemViewModel CreateRarityFilter()
        {
            var dummyFvm = new DummyFilterViewModel();

            var options = new List<FilterOption>
            {
                new FilterOption("common"),
                new FilterOption("uncommon"),
                new FilterOption("rare"),
                new FilterOption("mythic"),
                new FilterOption("bonus"),
            };

            // Construct the FilterItemViewModel for "Rarity". 
            return new FilterItemViewModel("Rarity", options, "Rarity ...", "Rarity", dummyFvm);
        }
        public static FilterItemViewModel CreateTypesFilter()
        {
            var dummyFvm = new DummyFilterViewModel();

            var options = new List<FilterOption>
            {
                new FilterOption("Artifact"),
                new FilterOption("Creature"),
                new FilterOption("Sorcery"),
                new FilterOption("Instant"),
                new FilterOption("Planeswalker"),
                new FilterOption("Enchantment"),
                new FilterOption("Land"),
                new FilterOption("Card"),

            };

            return new FilterItemViewModel("Types", options, "Types ...", "Types", dummyFvm);
        }

    }

    public class DummyFilterViewModel : FilterViewModel
    {
        public DummyFilterViewModel() : base(new CardViewModel()) { }
        public override void ApplyFiltering() { /* no-op */ }
        public override void DebugFullFilterState() { /* no-op */ }
    }
}
