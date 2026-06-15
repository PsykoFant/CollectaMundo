using CollectaMundo.ApplicationServices.KeyedDataProvider.Providers;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Shared.CardModels;

namespace CollectaMundo.Tests.TestUtils
{
    public static class TestCardFactory
    {
        public static void SeedSetMetaForTests(IEnumerable<PrintingCard> cards)
        {
            var dict = cards.Select(c => c.SetCode).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToDictionary(
                s => s!,
                s => new SetDto
                {
                    TokenCode = s!,
                    Code = s!,
                    Name = s!,
                    ReleaseDate = null
                },
                StringComparer.OrdinalIgnoreCase);

            CardDataProviders.SetMetaProvider = new SetDtoLookupProvider(dict);
        }
        public static PrintingCard CreatePrinting(
            string uuid,
            string oracleId = "oracle-test",
            string name = "Test Card",
            string setCode = "TST",
            string manaCost = "",
            string types = "",
            string colors = "",
            string superTypes = "",
            string subTypes = "",
            string type = "",
            string keywords = "",
            string text = "",
            double manaValue = 0,
            string language = "English",
            string finishes = "nonfoil",
            string rarity = "common",
            string? side = null,
            IEnumerable<string>? otherFaceIds = null)
        {
            return new PrintingCard
            {
                Uuid = uuid,
                SetCode = setCode,
                Language = language,
                Finishes = finishes,
                Rarity = rarity,
                Oracle = new OracleCard
                {
                    ScryfallOracleId = oracleId,
                    Name = name,
                    ManaCost = manaCost,
                    ManaCostRaw = manaCost,
                    Types = types,
                    Colors = colors,
                    SuperTypes = superTypes,
                    SubTypes = subTypes,
                    Type = type,
                    Keywords = keywords,
                    Text = text,
                    ManaValue = manaValue,
                    Side = side,
                    OtherFaceIds = otherFaceIds?.ToList() ?? []
                }
            };
        }
        public static List<PrintingCard> GetTestPrintings()
        {
            return
            [
                CreatePrinting("test-001", "oracle-001", "Davros, Dalek Creator", "WHO", "1,U,B,R", "Artifact, Creature", "B,R,U", "Legendary", "Alien, Scientist", "Legendary Artifact Creature — Alien Scientist", "Menace", "Menace\nAt the beginning...", 4, "English", "nonfoil, foil", "mythic"),
                CreatePrinting("test-002", "oracle-002", "Skeletal Swarming", "PRM", "3,B,G", "Enchantment", "B,G", "", "", "Enchantment", "", "Each Skeleton you control has trample, attacks each combat if able, and gets +X/+0, where X is the number of other Skeletons you control.\nAt the beginning of your end step, create a tapped 1/1 black Skeleton creature token. If a creature died this turn, create two of those tokens instead.", 5, "English", "nonfoil, foil", "rare"),
                CreatePrinting("test-003", "oracle-003", "Lumbering Laundry", "MKM", "5", "Artifact, Creature", "", "", "Golem", "Artifact Creature — Golem", "Disguise", "{2}: Until end of turn, you may look at face-down creatures you don't control any time.\nDisguise {5} (You may cast this card face down for {3} as a 2/2 creature with ward {2}. Turn it face up any time for its disguise cost.)", 5, "English", "nonfoil, foil", "uncommon"),
                CreatePrinting("test-004", "oracle-004", "Olivia Voldaren", "INR", "2,B,R", "Creature", "B,R", "Legendary", "Vampire", "Legendary Creature — Vampire", "Flying", "Flying\n{1}{R}: Olivia Voldaren deals 1 damage to another target creature. That creature becomes a Vampire in addition to its other types. Put a +1/+1 counter on Olivia Voldaren.\n{3}{B}{B}: Gain control of target Vampire for as long as you control Olivia Voldaren.", 4, "English", "nonfoil, foil", "mythic"),
                CreatePrinting("test-005", "oracle-005", "Rock Hydra", "30A", "X,R,R", "Creature", "R", "", "Hydra", "Creature — Hydra", "", "This creature enters with X +1/+1 counters on it.\nFor each 1 damage that would be dealt to this creature, if it has a +1/+1 counter on it, remove a +1/+1 counter from it and prevent that 1 damage.\n{R}: Prevent the next 1 damage that would be dealt to this creature this turn.\n{R}{R}{R}: Put a +1/+1 counter on this creature. Activate only during your upkeep.", 2, "English", "nonfoil", "rare"),
                CreatePrinting("test-006", "oracle-006", "Time Walk", "30A", "1,U", "Sorcery", "U", "", "", "Sorcery", "", "Take an extra turn after this one.", 2, "English", "nonfoil", "rare"),
                CreatePrinting("test-007", "oracle-007", "Struggle // Survive", "MOC", "2,R", "Instant", "R", "", "", "Instant", "Aftermath", "Struggle deals damage to target creature equal to the number of lands you control.", 5, "English", "nonfoil", "uncommon"),
                CreatePrinting("test-008", "oracle-008", "Lovestruck Beast // Heart's Desire", "CLB", "2,G", "Creature", "G", "", "Beast, Noble", "Creature — Beast Noble", "", "This creature can't attack unless you control a 1/1 creature.", 3, "English", "nonfoil", "rare"),
                CreatePrinting("test-009", "oracle-009", "Garruk Relentless // Garruk, the Veil-Cursed", "INR", "3,G", "Planeswalker", "G,B", "Legendary", "Garruk", "Legendary Planeswalker — Garruk", "Transform", "When Garruk has two or fewer loyalty counters on him, transform him.\n[0]: Garruk deals 3 damage to target creature. That creature deals damage equal to its power to him.\n[0]: Create a 2/2 green Wolf creature token.", 4, "English", "nonfoil, foil", "mythic"),
                CreatePrinting("test-010", "oracle-010", "Kozilek's Command", "MH3", "X,C,C", "Kindred, Instant", "", "", "Eldrazi", "Kindred Instant — Eldrazi", "", "Choose two —\n• Target player creates X 0/1 colorless Eldrazi Spawn creature tokens with \"Sacrifice this creature: Add {C}.\"\n• Target player scries X, then draws a card.\n• Exile target creature with mana value X or less.\n• Exile up to X target cards from graveyards.", 2, "English", "nonfoil, foil", "rare"),
                CreatePrinting("test-011", "oracle-011", "Propagator Drone", "MH3", "1,G", "Creature", "", "", "Eldrazi, Drone", "Creature — Eldrazi Drone", "Devoid", "Devoid (This card has no color.)\nCreature tokens you control have evolve. (They have \"Whenever a creature you control enters, if it has greater power or toughness than this token, put a +1/+1 counter on this token.\" They see this creature enter.)\n{3}{G}: Create a 0/1 colorless Eldrazi Spawn creature token with \"Sacrifice this token: Add {C}.\"", 2, "English", "nonfoil, foil", "uncommon"),
                CreatePrinting("test-012", "oracle-012", "Fire // Ice", "INV", "1,R", "Instant", "R,U", "", "", "Instant", "", "Fire deals 2 damage divided as you choose among one or two targets.", 4, "English", "nonfoil, foil", "uncommon"),
                CreatePrinting("test-013", "oracle-013", "Tarfire", "PLST", "R", "Kindred, Instant", "R", "", "Goblin", "Kindred Instant — Goblin", "", "Tarfire deals 2 damage to any target.", 1, "English", "nonfoil", "common"),
                CreatePrinting("test-014", "oracle-014", "Begin the Invasion", "MOC", "X,W,U,B,R,G", "Sorcery", "B,G,R,U,W", "", "", "Sorcery", "", "Search your library for up to X battle cards with different names, put them onto the battlefield, then shuffle.", 5, "English", "nonfoil, foil", "mythic"),
                CreatePrinting("test-015", "oracle-015", "Lukka, Bound to Ruin", "ONE", "2,R,R/G/P,G", "Planeswalker", "G,R", "Legendary", "Lukka", "Legendary Planeswalker — Lukka", "Compleated", "Compleated ({R/G/P} can be paid with {R}, {G}, or 2 life. If life was paid, this planeswalker enters with two fewer loyalty counters.)\n[+1]: Add {R}{G}. Spend this mana only to cast creature spells or activate abilities of creatures.\n[−1]: Create a 3/3 green Phyrexian Beast creature token with toxic 1.\n[−4]: Lukka deals X damage divided as you choose among any number of target creatures and/or planeswalkers, where X is the greatest power among creatures you control as you activate this ability.", 5, "English", "nonfoil, foil", "mythic"),
                CreatePrinting("test-016", "oracle-016", "Cat", "Aetherdrift", "", "Token, Creature", "W", "", "Cat", "Token Creature — Cat", "Lifelink", "Lifelink (Damage dealt by this creature also causes you to gain that much life.)", 0, "English", "nonfoil, foil", ""),
                CreatePrinting("test-017", "oracle-017", "Bounty: Eriana, Wrecking Ball // Wanted!", "OTC", "", "Card", "", "", "", "Card", "", "At the beginning of your end step, if you committed a crime this turn, collect your reward. (Targeting opponents, anything they control, and/or cards in their graveyards is a crime.)", 0, "English", "nonfoil, foil", ""),
                CreatePrinting("test-018", "oracle-018", "Tundra", "30A", "", "Land", "", "", "Plains,Island", "Land — Plains Island", "", "{T}: Add {W} or {U}.", 0, "English", "nonfoil", "rare")
            ];
        }
    }
}
