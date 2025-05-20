using CollectaMundo.ApplicationServices;
using CollectaMundo.Data;
using CollectaMundo.DomainLogic;
using CollectaMundo.DomainLogic.Models;
using CollectaMundo.UICoordinators;
using CollectaMundo.ViewModels;
using static CollectaMundo.MainWindow;

namespace CollectaMundo.Tests
{
    /// <summary>
    /// A single test‑class that keeps the same view‑model instances for all test
    /// methods.  We get one‑time async startup via <see cref="IAsyncLifetime"/>.
    /// </summary>
    public sealed class IntegrationTests :
        IClassFixture<InMemoryDatabaseFixture>,   // gets us the in‑memory DB
        IAsyncLifetime                            // lets us await async startup once
    {
        private readonly InMemoryDatabaseFixture _fx;
        private readonly EventHandler _refreshHandler;
        private static bool _isInitialised;
        private readonly IFilteringService _filteringCoordinator;
        private readonly ICardListRepository _cardListRepo;
        private readonly ICardListService _cardListCoordinator;
        private readonly EditCollectionViewModel _addVM;
        private readonly EditCollectionViewModel _editVM;
        private readonly List<CardChangeEventArgs> _changedEvents = [];

        // Shared state (one copy for the whole class/run)
        private static readonly CardViewModel _allCardsVM = new();
        private static readonly CardViewModel _myCollectionVM = new();
        private static readonly FilterViewModel _filterVM = new(new FilteringService(new FilterDefaultsRepository()));

        public IntegrationTests(InMemoryDatabaseFixture fixture)
        {
            _fx = fixture;
            _refreshHandler = RefreshFilteredLists;

            // **1)** point all DBAccess calls to the single in-memory connection
            DBAccess.connection = _fx.Connection;

            // your existing “read” wiring
            var filterDefaultsRepo = new FilterDefaultsRepository();
            _filteringCoordinator = new FilteringService(filterDefaultsRepo);
            _cardListRepo = new CardListRepository();
            _cardListCoordinator = new CardListService(_cardListRepo);

            // **2)** wire up the “write” side, *passing in* the same connection
            var editRepo = new EditCollectionRepository(_fx.Connection);
            var editLogic = new EditCollectionLogic(editRepo);
            var editUow = new UnitOfWork(_fx.Connection);
            var editService = new EditCollectionService(editUow, editLogic);

            _addVM = new EditCollectionViewModel(editService, removeCardWhenZero: true);
            _editVM = new EditCollectionViewModel(editService, removeCardWhenZero: false);

            // capture CardChanged events from both VMs
            _addVM.CardChanged += (_, e) => _changedEvents.Add(e);
            _editVM.CardChanged += (_, e) => _changedEvents.Add(e);
        }

        // IAsyncLifetime implementation
        public async Task InitializeAsync()
        {
            if (!_isInitialised)
            {
                _isInitialised = true;

                // populate your source lists
                await _cardListCoordinator.LoadAllCardsAsync(_allCardsVM.Cards);
                await _cardListCoordinator.LoadMyCollectionAsync(_myCollectionVM.Cards);

                // load filter defaults from the same in-memory DB
                await _filterVM.InitializeFilterDefaultsAsync();
                _filterVM.FilterChanged += _refreshHandler;
            }

            // always start tests with a clean filter slate
            _filterVM.ClearFiltersCommand.Execute(null);
            _refreshHandler(null, EventArgs.Empty);
        }
        private void RefreshFilteredLists(object? sender, EventArgs e)
        {
            _allCardsVM.FilteredCards = _filteringCoordinator.ApplyFilters(_allCardsVM.Cards, _filterVM.Filters.Values);
            _myCollectionVM.FilteredCards = _filteringCoordinator.ApplyFilters(_myCollectionVM.Cards, _filterVM.Filters.Values);
        }
        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public void Seed_has_expected_counts()
        {
            Assert.Equal(61, _allCardsVM.Cards.Count);
            Assert.Equal(22, _myCollectionVM.Cards.Count);
        }

        [Fact]
        public void CardViewModel_Object_Creation_Initialization()
        {

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
                "Resurrection",
                "Gisela, the Broken Blade // Brisela, Voice of Nightmares",
                "Sokrates, Athenian Teacher"
            };

            var actualAllCardsNames = _allCardsVM.Cards
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
            var actualMyCollectionNames = _myCollectionVM.Cards
                .Select(card => card.Name ?? string.Empty)
                .OrderBy(name => name)
                .ToList();
            var sortedMyCollectionExpected = expectedMyCollectionNames.OrderBy(name => name).ToList();
            Assert.Equal(sortedMyCollectionExpected, actualMyCollectionNames);

            // Assert: total number of cards you physically own in MyCollection is 43
            var totalCardsOwned = _myCollectionVM.Cards.Sum(c => c.CardsOwned);
            Assert.Equal(43, totalCardsOwned);

            // Assert: total number of cards you physically own in CardsForTrade is 6
            var totalCardsForTrade = _myCollectionVM.Cards.Sum(c => c.CardsForTrade);
            Assert.Equal(6, totalCardsForTrade);

            // Assert: 15 entries are marked as Near Mint condition
            var nearMintCount = _myCollectionVM.Cards.Count(c => string.Equals(c.SelectedCondition, "Near Mint", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(15, nearMintCount);

            // Assert: 2 entries are marked as Good condition
            var goodCount = _myCollectionVM.Cards.Count(c => string.Equals(c.SelectedCondition, "Good", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(2, goodCount);

            // Assert: 19 entries are marked as English language
            var englishCount = _myCollectionVM.Cards.Count(c => string.Equals(c.Language, "English", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(19, englishCount);

            // Assert: 2 entries are marked as French language
            var frenchCount = _myCollectionVM.Cards.Count(c => string.Equals(c.Language, "French", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(2, frenchCount);

            // Assert: 18 entries are marked as nonfoil finish
            var nonfoilCount = _myCollectionVM.Cards.Count(c => string.Equals(c.SelectedFinish, "nonfoil", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(18, nonfoilCount);

            // Assert: 3 entries are marked as foil finish
            var foilCount = _myCollectionVM.Cards.Count(c => string.Equals(c.SelectedFinish, "foil", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(3, foilCount);
        }

        [Fact]
        public void FilterViewModel_Object_Creation_Initialization()
        {
            var nameFilter = _filterVM.Filters["Name"];
            Assert.NotEmpty(nameFilter.FilterOptions);

            Assert.True(_filterVM.Filters.ContainsKey("SetName"), "Expected filter key 'SetName' not found.");
            var setNameFilter = _filterVM.Filters["SetName"];
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
                "Jan Jansen, Chaos Crafter // Jan Jansen, Chaos Crafter",
                "Gisela, the Broken Blade // Brisela, Voice of Nightmares",
                "Sokrates, Athenian Teacher"
            };

            // Assert that the filter options contain all expected names.
            Assert.True(expectedNames.All(expected =>
                nameFilter.FilterOptions.Any(opt => opt.OptionName.Contains(expected))),
                "Not all expected filter names were found.");

            // Rarity:
            var rarityFilter = _filterVM.Filters["Rarity"];
            var expectedRarityOptions = new List<string> { "common", "uncommon", "rare", "mythic" };

            var actualRarityOptions = rarityFilter.FilterOptions
                .Select(opt => opt.OptionName)
                .OrderBy(x => x)
                .ToList();

            var sortedExpectedRarityOptions = expectedRarityOptions.OrderBy(x => x).ToList();
            Assert.Equal(sortedExpectedRarityOptions, actualRarityOptions);


            // Keywords:
            var keywordsFilter = _filterVM.Filters["Keywords"];
            var expectedKeywordsOptions = new List<string>
            {
                "Enrage",
                "Flash",
                "First strike",
                "Devoid",
                "Flying",
                "Evoke",
                "Haste",
                "Kicker",
                "Enchant",
                "Landfall",
                "Lifelink",
                "Meld",
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
                "Sokratic Dialogue",
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
            var subTypesFilter = _filterVM.Filters["SubTypes"];
            var expectedSubtypesOptions = new List<string>
            {
                "Advisor",
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
                "Horror",
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
            var subTypesLabelFilter = _filterVM.Filters["SubTypes"];
            Assert.Equal("Subtypes", subTypesLabelFilter.ReadableLabel);


            // SelectedCondition:
            var selectedConditionFilter = _filterVM.Filters["SelectedCondition"];
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
            var selectedFinishFilter = _filterVM.Filters["SelectedFinish"];
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
            var selectedFinishLabelFilter = _filterVM.Filters["SelectedFinish"];
            Assert.Equal("Chosen finish", selectedFinishLabelFilter.ReadableLabel);

            // Language:
            var selectedLanguageFilter = _filterVM.Filters["Language"];
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
            var colorFilter = _filterVM.Filters["Colors"];
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
            var manaValueFilter = _filterVM.Filters["ManaValue"];
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
        public void Filter_Integration_Test_Simple()
        {
            // Arrange: Filter on ManaValue > 1.
            var numericFilter = _filterVM.Filters["ManaValue"];
            numericFilter.SelectedNumericValue = 1;
            numericFilter.OperatorSelection = OperatorType.GREATER_THAN;

            // Filter on Rarity not being mythic or rare.
            var rarityFilter = _filterVM.Filters["Rarity"];
            foreach (var opt in rarityFilter.FilterOptions.Where(o => o.OptionName is "mythic" or "rare"))
            {
                opt.IsSelected = true;          // this setter calls NotifyFilterChanged
            }
            rarityFilter.OperatorSelection = OperatorType.NOT;

            // Act: Apply filtering to TestAllCardsVM and TestMyCollectionVM.
            _allCardsVM.FilteredCards = _filteringCoordinator.ApplyFilters(_allCardsVM.Cards, _filterVM.Filters.Values);
            var filteredAllCards = _allCardsVM.FilteredCards;

            _myCollectionVM.FilteredCards = _filteringCoordinator.ApplyFilters(_myCollectionVM.Cards, _filterVM.Filters.Values);
            var filteredMyCollection = _myCollectionVM.FilteredCards;

            // Assert: Expected summary string
            string expectedSummary = "Rarity: {NOT mythic AND NOT rare} AND ManaValue > 1";
            Assert.Equal(expectedSummary, _filterVM.FilterSummary);

            // Assert: Number of cards in filteredAllCards and filteredMyCollection.
            Assert.Equal(22, filteredAllCards.Count);
            Assert.Equal(17, filteredMyCollection.Count);

            // Arrange: Add color filters to existing filters.
            var colorFilter = _filterVM.Filters["Colors"];
            foreach (var opt in colorFilter.FilterOptions.Where(o => o.OptionName is "R" or "G"))
            {
                opt.IsSelected = true;          // this setter calls NotifyFilterChanged
            }
            colorFilter.OperatorSelection = OperatorType.OR;

            // Act: Apply filtering to TestAllCardsVM and TestMyCollectionVM.
            _allCardsVM.FilteredCards = _filteringCoordinator.ApplyFilters(_allCardsVM.Cards, _filterVM.Filters.Values);
            filteredAllCards = _allCardsVM.FilteredCards;

            _myCollectionVM.FilteredCards = _filteringCoordinator.ApplyFilters(_myCollectionVM.Cards, _filterVM.Filters.Values);
            filteredMyCollection = _myCollectionVM.FilteredCards;

            // Assert: Expected summary string
            expectedSummary = "Colors: {R OR G} AND Rarity: {NOT mythic AND NOT rare} AND ManaValue > 1";
            Assert.Equal(expectedSummary, _filterVM.FilterSummary);

            // Assert: Number of cards in filteredAllCards and filteredMyCollection.
            Assert.Equal(12, filteredAllCards.Count);
            Assert.Equal(10, filteredMyCollection.Count);
        }

        [Fact]
        public void Filter_Integration_Test_Scenario_With_Event_Subscription()
        {

            // Act: Apply first filter – Name contains "Ranger"
            var nameFilter = _filterVM.Filters["Name"];
            nameFilter.SelectedSingleOption = "Ranger";

            // Assert: only the two “Ranger” cards appear, none in MyCollection
            var expectedNames = new List<string> { "Boundary Lands Ranger", "Ranger-Captain of Eos // Ranger-Captain of Eos" }.OrderBy(n => n).ToList();

            var actualNames = _allCardsVM.FilteredCards
                               .Select(c => c.Name!)   // names are non‑null in seed data
                               .OrderBy(n => n)
                               .ToList();

            Assert.Equal(expectedNames, actualNames);
            Assert.Empty(_myCollectionVM.FilteredCards);

            // Arrange: Clear all filters via the command 
            _filterVM.ClearFiltersCommand?.Execute(null);   // raises FilterChanged again

            // Assert: lists are back to their full size and summary text is empty
            Assert.Equal(61, _allCardsVM.FilteredCards.Count);
            Assert.Equal(22, _myCollectionVM.FilteredCards.Count);
            Assert.True(string.IsNullOrEmpty(_filterVM.FilterSummary));

            // Act: Start over with filtering – filter on rules text
            var rulesFilter = _filterVM.Filters["Text"];
            rulesFilter.SelectedSingleOption = "+1/+1 counter";

            // Assert: three cards in AllCards, two in MyCollection with +1/+1 counter in their rules text
            Assert.Equal(3, _allCardsVM.FilteredCards.Count);
            Assert.Equal(2, _myCollectionVM.FilteredCards.Count);

            // Act: Add one more filter - SetName contains "The List"
            var setFilter = _filterVM.Filters["SetName"];
            setFilter.SelectedSingleOption = "The List";
            _filterVM.NotifyFilterChanged();

            // Assert: two cards in AllCards, two in MyCollection with +1/+1 counter in their rules text and from the set "The List"
            Assert.Equal(2, _allCardsVM.FilteredCards.Count);
            Assert.Equal(2, _myCollectionVM.FilteredCards.Count);
            Assert.Equal("SetName: \"The List\" AND Text: \"+1/+1 counter\"", _filterVM.FilterSummary);


            // Arrange: Clear all filters via the command for the second time
            _filterVM.ClearFiltersCommand?.Execute(null);   // raises FilterChanged again

            // Assert: lists are back to their full size and summary text is empty
            Assert.Equal(61, _allCardsVM.FilteredCards.Count);
            Assert.Equal(22, _myCollectionVM.FilteredCards.Count);
            Assert.True(string.IsNullOrEmpty(_filterVM.FilterSummary));

            // Act: Add a filter on the "Types" filter - select "Creature" and "Planeswalker"
            var typesFilter = _filterVM.Filters["Types"];
            foreach (var opt in typesFilter.FilterOptions.Where(o => o.OptionName is "Creature" or "Planeswalker"))
            {
                opt.IsSelected = true;
            }
            typesFilter.OperatorSelection = OperatorType.OR;

            // Assert: 25 cards in AllCards, 10 in MyCollection with type "Creature" or "Planeswalker"
            Assert.Equal(27, _allCardsVM.FilteredCards.Count);
            Assert.Equal(10, _myCollectionVM.FilteredCards.Count);

            // Act: Add a filter on the "SuperTypes" filter - select "Legendary"
            var superTypesFilter = _filterVM.Filters["SuperTypes"];
            foreach (var opt in superTypesFilter.FilterOptions.Where(o => o.OptionName is "Legendary"))
            {
                opt.IsSelected = true;
            }

            // Assert: 3 cards in AllCards, none in MyCollection with type "Creature" or "Planeswalker" and supertype "Legendary"
            Assert.Equal(5, _allCardsVM.FilteredCards.Count);
            Assert.Empty(_myCollectionVM.FilteredCards);
            Assert.Equal("SuperTypes: {Legendary} AND Types: {Creature OR Planeswalker}", _filterVM.FilterSummary);


            // 1) pick the one FilteredCards item you want
            var uuidToAdd = "e4dcfe4f-8441-5eec-9f74-a7b3672e90e0";
            var cardToAdd = _allCardsVM.FilteredCards.Single(c => c.Uuid == uuidToAdd);

            // 2) “fake” the DataGrid selection by wrapping it in an object‐array
            var selection = new object[] { cardToAdd };

            // 3) call the command exactly as the UI would
            _addVM.AddSelectedCardsCommand.Execute(selection);

            // at that point addVM.CardsToAdd contains your card
            Assert.Single(_addVM.CardsToAdd, c => c.Uuid == uuidToAdd);

            _addVM.SubmitNewCardsCommand.Execute(null);
            Assert.Equal(23, _myCollectionVM.Cards.Count);

        }

    }
}

