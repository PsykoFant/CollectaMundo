using CollectaMundo.Managers;
using CollectaMundo.ViewModels;
using static CollectaMundo.MainWindow;

namespace CollectaMundo.Tests
{
    public class IntegrationTests : IClassFixture<InMemoryDatabaseFixture>
    {
        private readonly InMemoryDatabaseFixture _fixture;

        // Real instances of view model objects.
        public CardViewModel TestAllCardsVM { get; } = new CardViewModel();
        public CardViewModel TestMyCollectionVM { get; } = new CardViewModel();
        public FilterViewModel TestFilterVM { get; } = new FilterViewModel();

        public IntegrationTests(InMemoryDatabaseFixture fixture)
        {
            _fixture = fixture;
            // Ensure that the static DBAccess.connection points to our in-memory connection.
            DBAccess.connection = _fixture.Connection;
        }

        private async Task InitializeTestObjectsAsync()
        {
            // Populate the AllCards and MyCollection card lists.
            await CardListManager.CreateCardListObjectAsync(TestAllCardsVM.Cards, CardListObject.AllCards);
            await CardListManager.CreateCardListObjectAsync(TestMyCollectionVM.Cards, CardListObject.MyCollection);
            await TestFilterVM.InitializeFilterDefaultsAsync();
        }

        [Fact]
        public async Task CardViewModel_Object_Creation_Initialization()
        {
            await InitializeTestObjectsAsync();

            // Assert: Check that the seed data was loaded.
            Assert.NotEmpty(TestAllCardsVM.Cards);
            Assert.NotEmpty(TestMyCollectionVM.Cards);

            // Assert: CardViewModel objects have the expected number of cards.
            Assert.Equal(59, TestAllCardsVM.Cards.Count);
            Assert.Equal(22, TestMyCollectionVM.Cards.Count);

            // Assert: Both CardViewModel objects have the expected names
            var expectedAllCardsNames = new List<string>
            {
                "Boundary Lands Ranger",
                "Island // Island",
                "Ancient Greenwarden",
                "Warriors",
                "Devil",
                "Otter",
                "Season of Weaving // Season of Weaving",
                "Rampant Frogantua // Rampant Frogantua",
                "Goblin",
                "Dog",
                "Prismatic Vista",
                "The Thirteenth Doctor",
                "Cat",
                "All Will Be One // All Will Be One",
                "Jan Jansen, Chaos Crafter // Jan Jansen, Chaos Crafter",
                "Bloodvial Purveyor // Bloodvial Purveyor",
                "Forest",
                "Unblinking Observer // Unblinking Observer",
                "Prismatic Ending",
                "Sythis, Harvest's Hand // Sythis, Harvest's Hand",
                "Blossoming Calm // Blossoming Calm",
                "Shadrix Silverquill // Shadrix Silverquill",
                "Realmwalker",
                "Deftblade Elite",
                "Snapping Sailback",
                "Dragonscale Boon",
                "Flameshot",
                "Nissa, Steward of Elements",
                "Staying Power",
                "Deny the Divine",
                "Once Upon a Time",
                "Silent Clearing // Silent Clearing",
                "Ranger-Captain of Eos // Ranger-Captain of Eos",
                "Chillerpillar // Chillerpillar",
                "Dead Weight",
                "Karox Bladewing",
                "Bubbling Cauldron",
                "Thought Harvester",
                "Culling Drone",
                "Plummet",
                "Font of Ire",
                "Guild Feud",
                "Angel of Glory's Rise",
                "Zombie",
                "Grazing Gladehart",
                "Plains",
                "Glarewielder",
                "Leave No Trace",
                "Ouphe Vandals",
                "Syphon Soul",
                "Gixian Puppeteer",
                "Hypnotic Cloud",
                "Crenellated Wall",
                "Renounce",
                "Viashino Runner",
                "Hungry Mist",
                "Vexing Arcanix",
                "Thallid Devourer",
                "Resurrection"
            };

            var actualAllCardsNames = TestAllCardsVM.Cards
                .Select(card => card.Name ?? string.Empty)
                .OrderBy(name => name)
                .ToList();
            var sortedAllcardsExpected = expectedAllCardsNames.OrderBy(name => name).ToList();
            Assert.Equal(sortedAllcardsExpected, actualAllCardsNames);

            var expectedMyCollectionNames = new List<string>
            {
                "Prismatic Ending",
                "Snapping Sailback",
                "Dragonscale Boon",
                "Once Upon a Time",
                "Chillerpillar // Chillerpillar",
                "Thought Harvester",
                "Culling Drone",
                "Plummet",
                "Font of Ire",
                "Guild Feud",
                "Grazing Gladehart",
                "Glarewielder",
                "Leave No Trace",
                "Ouphe Vandals",
                "Syphon Soul",
                "Hypnotic Cloud",
                "Crenellated Wall",
                "Viashino Runner",
                "Hungry Mist",
                "Vexing Arcanix",
                "Thallid Devourer",
                "Resurrection"
            };
            var actualMyCollectionNames = TestMyCollectionVM.Cards
                .Select(card => card.Name ?? string.Empty)
                .OrderBy(name => name)
                .ToList();
            var sortedMyCollectionExpected = expectedMyCollectionNames.OrderBy(name => name).ToList();
            Assert.Equal(sortedMyCollectionExpected, actualMyCollectionNames);

            // Assert: total number of cards you physically own in MyCollection is 43
            var totalCardsOwned = TestMyCollectionVM.Cards.Sum(c => c.CardsOwned);
            Assert.Equal(43, totalCardsOwned);

            // Assert: total number of cards you physically own in CardsForTrade is 6
            var totalCardsForTrade = TestMyCollectionVM.Cards.Sum(c => c.CardsForTrade);
            Assert.Equal(6, totalCardsForTrade);

            // Assert: 15 entries are marked as Near Mint condition
            var nearMintCount = TestMyCollectionVM.Cards
                .Count(c => string.Equals(c.SelectedCondition, "Near Mint", StringComparison.OrdinalIgnoreCase));

            Assert.Equal(15, nearMintCount);

            // Assert: 2 entries are marked as Good condition
            var goodCount = TestMyCollectionVM.Cards
                .Count(c => string.Equals(c.SelectedCondition, "Good", StringComparison.OrdinalIgnoreCase));

            Assert.Equal(2, goodCount);

            // Assert: 19 entries are marked as English language
            var englishCount = TestMyCollectionVM.Cards
                .Count(c => string.Equals(c.Language, "English", StringComparison.OrdinalIgnoreCase));

            Assert.Equal(19, englishCount);

            // Assert: 2 entries are marked as French language
            var frenchCount = TestMyCollectionVM.Cards
                .Count(c => string.Equals(c.Language, "French", StringComparison.OrdinalIgnoreCase));

            Assert.Equal(2, frenchCount);

            // Assert: 18 entries are marked as nonfoil finish
            var nonfoilCount = TestMyCollectionVM.Cards
                .Count(c => string.Equals(c.SelectedFinish, "nonfoil", StringComparison.OrdinalIgnoreCase));

            Assert.Equal(18, nonfoilCount);

            // Assert: 3 entries are marked as foil finish
            var foilCount = TestMyCollectionVM.Cards
                .Count(c => string.Equals(c.SelectedFinish, "foil", StringComparison.OrdinalIgnoreCase));

            Assert.Equal(3, foilCount);
        }

        [Fact]
        public async Task FilterViewModel_Object_Creation_Initialization()
        {
            await InitializeTestObjectsAsync();

            Assert.True(TestFilterVM.Filters.ContainsKey("Name"), "Expected filter key 'Name' not found.");
            var nameFilter = TestFilterVM.Filters["Name"];
            Assert.NotEmpty(nameFilter.FilterOptions);

            Assert.True(TestFilterVM.Filters.ContainsKey("SetName"), "Expected filter key 'SetName' not found.");
            var setNameFilter = TestFilterVM.Filters["SetName"];
            Assert.NotEmpty(setNameFilter.FilterOptions);

            // Hardcoded lists of all expected names for the test.
            var expectedNames = new List<string>
            {
                "Once Upon a Time",
                "Snapping Sailback",
                "Dragonscale Boon",
                "Gixian Puppeteer",
                "Thought Harvester",
                "Glarewielder",
                "Plummet",
                "Hypnotic Cloud",
                "Dead Weight",
                "Grazing Gladehart",
                "Leave No Trace",
                "Realmwalker",
                "Vexing Arcanix",
                "Deny the Divine",
                "Resurrection",
                "Ancient Greenwarden",
                "Thallid Devourer",
                "Hungry Mist",
                "Syphon Soul",
                "Bubbling Cauldron",
                "The Thirteenth Doctor",
                "Forest",
                "Deftblade Elite",
                "Viashino Runner",
                "Angel of Glory's Rise",
                "Staying Power",
                "Flameshot",
                "Prismatic Ending",
                "Boundary Lands Ranger",
                "Ouphe Vandals",
                "Guild Feud",
                "Plains",
                "Font of Ire",
                "Prismatic Vista",
                "Crenellated Wall",
                "Renounce",
                "Nissa, Steward of Elements",
                "Culling Drone",
                "Zombie",
                "Bloodvial Purveyor // Bloodvial Purveyor",
                "Warriors",
                "Cat",
                "Unblinking Observer // Unblinking Observer",
                "Otter",
                "Island // Island",
                "Dog",
                "Ranger-Captain of Eos // Ranger-Captain of Eos",
                "Silent Clearing // Silent Clearing",
                "All Will Be One // All Will Be One",
                "Shadrix Silverquill // Shadrix Silverquill",
                "Rampant Frogantua // Rampant Frogantua",
                "Season of Weaving // Season of Weaving",
                "Chillerpillar // Chillerpillar",
                "Devil",
                "Sythis, Harvest's Hand // Sythis, Harvest's Hand",
                "Karox Bladewing",
                "Blossoming Calm // Blossoming Calm",
                "Goblin",
                "Jan Jansen, Chaos Crafter // Jan Jansen, Chaos Crafter"
            };

            // Assert that the filter options contain all expected names.
            Assert.True(expectedNames.All(expected =>
                nameFilter.FilterOptions.Any(opt => opt.OptionName.Contains(expected))),
                "Not all expected filter names were found.");

            // Rarity:
            var rarityFilter = TestFilterVM.Filters["Rarity"];
            var expectedRarityOptions = new List<string> { "common", "uncommon", "rare", "mythic" };

            var actualRarityOptions = rarityFilter.FilterOptions
                .Select(opt => opt.OptionName)
                .OrderBy(x => x)
                .ToList();

            var sortedExpectedRarityOptions = expectedRarityOptions.OrderBy(x => x).ToList();
            Assert.Equal(sortedExpectedRarityOptions, actualRarityOptions);


            // Keywords:
            var keywordsFilter = TestFilterVM.Filters["Keywords"];
            var expectedKeywordsOptions = new List<string>
            {
                "Enrage",
                "Flash",
                "Devoid",
                "Flying",
                "Evoke",
                "Haste",
                "Kicker",
                "Enchant",
                "Landfall",
                "Radiance",
                "Changeling",
                "Reach",
                "Paradox",
                "Team TARDIS",
                "Provoke",
                "Menace",
                "Converge",
                "Fight",
                "Defender",
                "Scry",
                "Ingest",
                "Prowess"
            };
            var expectedKeyWordsOperators = new[]
            {
                OperatorType.OR,
                OperatorType.AND,
                OperatorType.NOT
            };

            var actualKeywordsOptions = keywordsFilter.FilterOptions
                .Select(opt => opt.OptionName)
                .OrderBy(x => x)
                .ToList();

            var sortedExpectedKeywordsOptions = expectedKeywordsOptions.OrderBy(x => x).ToList();
            Assert.Equal(sortedExpectedKeywordsOptions, actualKeywordsOptions);
            Assert.Equal(expectedKeyWordsOperators, [.. keywordsFilter.AvailableOperators!]);

            // Subtypes:
            var subTypesFilter = TestFilterVM.Filters["SubTypes"];
            var expectedSubtypesOptions = new List<string>
            {
                "Angel",
                "Antelope",
                "Aura",
                "Cat",
                "Devil",
                "Dinosaur",
                "Doctor",
                "Dog",
                "Dragon",
                "Drone",
                "Eldrazi",
                "Elemental",
                "Forest",
                "Fungus",
                "Goblin",
                "Human",
                "Lizard",
                "Nissa",
                "Otter",
                "Ouphe",
                "Phyrexian",
                "Plains",
                "Ranger",
                "Rogue",
                "Shaman",
                "Shapeshifter",
                "Soldier",
                "Time Lord",
                "Wall",
                "Warlock",
                "Zombie"
            };

            var actualSubTypesOptions = subTypesFilter.FilterOptions
                .Select(opt => opt.OptionName)
                .OrderBy(x => x)
                .ToList();

            var sortedExpectedSubTypesOptions = expectedSubtypesOptions.OrderBy(x => x).ToList();
            Assert.Equal(sortedExpectedSubTypesOptions, actualSubTypesOptions);

            // Assert: the readable label for the "SubTypes" filter is "Subtypes"
            var subTypesLabelFilter = TestFilterVM.Filters["SubTypes"];
            Assert.Equal("Subtypes", subTypesLabelFilter.ReadableLabel);


            // SelectedCondition:
            var selectedConditionFilter = TestFilterVM.Filters["SelectedCondition"];
            var expectedSelectedConditionsOptions = new List<string>
            {
                "Near Mint",
                "Excellent",
                "Light Played",
                "Good",
                "Poor",
                "Mint"
            };

            var actualSelectedConditionsOptions = selectedConditionFilter.FilterOptions
                .Select(opt => opt.OptionName)
                .OrderBy(x => x)
                .ToList();

            var sortedExpectedSelectedConditionsOptions = expectedSelectedConditionsOptions.OrderBy(x => x).ToList();
            Assert.Equal(sortedExpectedSelectedConditionsOptions, actualSelectedConditionsOptions);

            // SelectedFinish:
            var selectedFinishFilter = TestFilterVM.Filters["SelectedFinish"];
            var expectedSelectedFinishOptions = new List<string>
            {
                "etched",
                "nonfoil",
                "foil"
            };

            var actualSelectedFinishOptions = selectedFinishFilter.FilterOptions
                .Select(opt => opt.OptionName)
                .OrderBy(x => x)
                .ToList();

            var sortedExpectedSelectedFinishOptions = expectedSelectedFinishOptions.OrderBy(x => x).ToList();
            Assert.Equal(sortedExpectedSelectedFinishOptions, actualSelectedFinishOptions);

            // Assert: the readable label for the "SelectedFinish" filter is "Chosen finish"
            var selectedFinishLabelFilter = TestFilterVM.Filters["SelectedFinish"];
            Assert.Equal("Chosen finish", selectedFinishLabelFilter.ReadableLabel);

            // Language:
            var selectedLanguageFilter = TestFilterVM.Filters["Language"];
            var expectedLanguageOptions = new List<string>
            {
                "French",
                "English",
                "German"
            };

            var actualLanguageOptions = selectedLanguageFilter.FilterOptions
                .Select(opt => opt.OptionName)
                .OrderBy(x => x)
                .ToList();

            var sortedExpectedLanguageOptions = expectedLanguageOptions.OrderBy(x => x).ToList();
            Assert.Equal(sortedExpectedLanguageOptions, actualLanguageOptions);

            // Colors:
            var colorFilter = TestFilterVM.Filters["Colors"];
            var expectedColorOptions = new List<string>
            {
                "W", "U", "B", "R", "G", "C", "X", "Colorless"
            };

            var actualColorOptions = colorFilter.FilterOptions
                .Select(opt => opt.OptionName)
                .OrderBy(x => x)
                .ToList();

            var sortedExpectedColorOptions = expectedColorOptions.OrderBy(x => x).ToList();
            Assert.Equal(sortedExpectedColorOptions, actualColorOptions);

            // ManaValue:
            var manaValueFilter = TestFilterVM.Filters["ManaValue"];
            var expectedManaValueOptions = new List<string>
            {
                "0", "1", "2", "3", "4", "5", "6", "7"
            };

            var expectedManaValueOperators = new[]
            {
                OperatorType.GREATER_THAN,
                OperatorType.LESS_THAN,
                OperatorType.EQUALS,
                OperatorType.GREATER_THAN_OR_EQUALS,
                OperatorType.LESS_THAN_OR_EQUALS
            };

            var actualManaValueOptions = manaValueFilter.FilterOptions
                .Select(opt => opt.OptionName)
                .OrderBy(x => x)
                .ToList();

            var sortedExpectedManavalueOptions = expectedManaValueOptions.OrderBy(x => x).ToList();
            Assert.Equal(sortedExpectedManavalueOptions, actualManaValueOptions);
            Assert.Equal(expectedManaValueOperators, [.. manaValueFilter.AvailableOperators!]);
        }

        [Fact]
        public async Task Test_CombinedNameAndNumericFilter_Integration()
        {
            // Arrange: Initialize all view models.
            await InitializeTestObjectsAsync();

            // Filter on ManaValue > 1.
            var numericFilter = TestFilterVM.Filters["ManaValue"];
            numericFilter.SelectedNumericValue = 1;
            numericFilter.OperatorSelection = OperatorType.GREATER_THAN;

            // Filter on Rarity not being mythic or rare.
            var rarityFilter = TestFilterVM.Filters["Rarity"];
            foreach (var opt in rarityFilter.FilterOptions.Where(o => o.OptionName is "mythic" or "rare"))
            {
                opt.IsSelected = true;          // this setter calls NotifyFilterChanged
            }
            rarityFilter.OperatorSelection = OperatorType.NOT;

            // Act: Apply filtering to TestAllCardsVM and TestMyCollectionVM.
            TestAllCardsVM.FilteredCards = FilterManager.ApplyFilter(TestAllCardsVM.Cards, TestFilterVM.Filters.Values);
            var filteredAllCards = TestAllCardsVM.FilteredCards;

            TestMyCollectionVM.FilteredCards = FilterManager.ApplyFilter(TestMyCollectionVM.Cards, TestFilterVM.Filters.Values);
            var filteredMyCollection = TestMyCollectionVM.FilteredCards;

            // Assert: Expected summary string
            string expectedSummary = "Rarity: {NOT mythic AND NOT rare} AND ManaValue > 1";
            Assert.Equal(expectedSummary, TestFilterVM.FilterSummary);

            // Assert: Number of cards in filteredAllCards and filteredMyCollection.
            Assert.Equal(22, filteredAllCards.Count);
            Assert.Equal(17, filteredMyCollection.Count);

            // Arrange: Add color filters to existing filters.
            var colorFilter = TestFilterVM.Filters["Colors"];
            foreach (var opt in colorFilter.FilterOptions.Where(o => o.OptionName is "R" or "G"))
            {
                opt.IsSelected = true;          // this setter calls NotifyFilterChanged
            }
            colorFilter.OperatorSelection = OperatorType.OR;

            // Act: Apply filtering to TestAllCardsVM and TestMyCollectionVM.
            TestAllCardsVM.FilteredCards = FilterManager.ApplyFilter(TestAllCardsVM.Cards, TestFilterVM.Filters.Values);
            filteredAllCards = TestAllCardsVM.FilteredCards;

            TestMyCollectionVM.FilteredCards = FilterManager.ApplyFilter(TestMyCollectionVM.Cards, TestFilterVM.Filters.Values);
            filteredMyCollection = TestMyCollectionVM.FilteredCards;

            // Assert: Expected summary string
            expectedSummary = "Colors: {R OR G} AND Rarity: {NOT mythic AND NOT rare} AND ManaValue > 1";
            Assert.Equal(expectedSummary, TestFilterVM.FilterSummary);

            // Assert: Number of cards in filteredAllCards and filteredMyCollection.
            Assert.Equal(12, filteredAllCards.Count);
            Assert.Equal(10, filteredMyCollection.Count);
        }
    }
}
