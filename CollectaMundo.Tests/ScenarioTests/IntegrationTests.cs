using CollectaMundo.ApplicationServices.Filtering;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.CardLocations.Models;
using CollectaMundo.DomainLogic.Filtering.Enums;
using CollectaMundo.Infrastructure.Shared;
using CollectaMundo.Tests.TestUtils;
using CollectaMundo.ViewModels;
using CollectaMundo.ViewModels.ModifyCollection;
using System.Data.SQLite;
using System.Diagnostics;
using System.Windows.Input;

namespace CollectaMundo.Tests.ScenarioTests
{
    public sealed class ImmediateScheduler : IFacetUpdateScheduler
    {
        public void Schedule(Action run) => run();
        public void Cancel() { }
    }
    public sealed class SeedIntegrationTests(InMemoryDatabaseFixture fx) : IClassFixture<InMemoryDatabaseFixture>, IAsyncLifetime
    {
        private IDbConnectionFactory _dbFactory = null!;
        private MainWindowViewModel _mainVM = null!;
        private readonly InMemoryDatabaseFixture _fx = fx;

        public async ValueTask InitializeAsync()
        {
            _dbFactory = SharedMemoryDbFactory.CreateInMemoryDbFactory(_fx.DbName);
            (_mainVM, _) = await TestAppBuilder.BuildAsync(_fx, _dbFactory);
        }
        public ValueTask DisposeAsync()
        {
            _mainVM.Dispose();
            return ValueTask.CompletedTask;
        }

        [Fact]
        public void Seed_has_expected_counts()
        {
            var allCards = _mainVM.AllCardsVM.Cards;
            var myCollection = _mainVM.MyCollectionVM.Cards;

            Assert.Equal(65, allCards.Count);
            Assert.Equal(22, myCollection.Count);
        }
    }
    public sealed class CardViewModelIntegrationTests : IClassFixture<InMemoryDatabaseFixture>, IAsyncLifetime
    {
        private IDbConnectionFactory _dbFactory = null!;
        private MainWindowViewModel _mainVM = null!;
        private readonly InMemoryDatabaseFixture _fx;
        public CardViewModelIntegrationTests(InMemoryDatabaseFixture fx) => _fx = fx;
        public async ValueTask InitializeAsync()
        {
            _dbFactory = SharedMemoryDbFactory.CreateInMemoryDbFactory(_fx.DbName);
            (_mainVM, _) = await TestAppBuilder.BuildAsync(_fx, _dbFactory);
        }
        public ValueTask DisposeAsync()
        {
            _mainVM.Dispose();
            return ValueTask.CompletedTask;
        }

        [Fact]
        public void CardViewModel_Object_Creation_Initialization()
        {

            // Assert: Both CardListViewModel objects have the expected names
            var expectedAllCardsNames = new List<string>
            {
                "Boundary Lands Ranger",
                "Bruna, the Fading Light // Brisela, Voice of Nightmares",
                "Bloom Tender // Bloom Tender",
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
                "Viashino Runner",
                "Hungry Mist",
                "Vexing Arcanix",
                "Thallid Devourer",
                "Resurrection",
                "Gisela, the Broken Blade // Brisela, Voice of Nightmares",
                "Sokrates, Athenian Teacher",
                "Never // Return"
            };

            var actualAllCardsNames = _mainVM.AllCardsVM.Cards.Select(card => card.Name ?? string.Empty).OrderBy(name => name).ToList();
            var sortedAllcardsExpected = expectedAllCardsNames.OrderBy(name => name).ToList();

            for (int i = 0; i < sortedAllcardsExpected.Count; i++)
            {
                Debug.WriteLine($"Comparing index {i}:");
                Debug.WriteLine($"Expected: '{sortedAllcardsExpected[i]}'");
                Debug.WriteLine($"Actual:   '{actualAllCardsNames[i]}'");

                var expected = sortedAllcardsExpected[i];
                var actual = actualAllCardsNames[i];

                if (expected != actual)
                {
                    Debug.WriteLine($"Mismatch at index {i}:\nExpected: '{expected}'\nActual:   '{actual}'");
                    Debug.WriteLine($"Expected (UTF-16): {string.Join(" ", expected.Select(c => ((int)c).ToString("X4")))}");
                    Debug.WriteLine($"Actual   (UTF-16): {string.Join(" ", actual.Select(c => ((int)c).ToString("X4")))}");
                }

                Assert.Equal(expected, actual); // keep the original assertion
            }
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
            var actualMyCollectionNames = _mainVM.MyCollectionVM.Cards.Select(card => card.Name ?? string.Empty).OrderBy(name => name).ToList();
            var sortedMyCollectionExpected = expectedMyCollectionNames.OrderBy(name => name).ToList();
            Assert.Equal(sortedMyCollectionExpected, actualMyCollectionNames);

            // Assert: total number of cards you physically own in MyCollection is 43
            var totalCardsOwned = _mainVM.MyCollectionVM.Cards.Sum(c => c.CardsOwned);
            Assert.Equal(43, totalCardsOwned);

            // Assert: total number of cards you physically own in CardsForTrade is 6
            var totalCardsForTrade = _mainVM.MyCollectionVM.Cards.Sum(c => c.CardsForTrade);
            Assert.Equal(6, totalCardsForTrade);

            // Assert: 15 entries are marked as Near Mint condition
            var nearMintCount = _mainVM.MyCollectionVM.Cards.Count(c => string.Equals(c.SelectedCondition, "Near Mint", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(15, nearMintCount);

            // Assert: 2 entries are marked as Good condition
            var goodCount = _mainVM.MyCollectionVM.Cards.Count(c => string.Equals(c.SelectedCondition, "Good", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(2, goodCount);

            // Assert: 19 entries are marked as English language
            var englishCount = _mainVM.MyCollectionVM.Cards.Count(c => string.Equals(c.Language, "English", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(19, englishCount);

            // Assert: 2 entries are marked as French language
            var frenchCount = _mainVM.MyCollectionVM.Cards.Count(c => string.Equals(c.Language, "French", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(2, frenchCount);

            // Assert: 18 entries are marked as nonfoil finish
            var nonfoilCount = _mainVM.MyCollectionVM.Cards.Count(c => string.Equals(c.SelectedFinish, "nonfoil", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(18, nonfoilCount);

            // Assert: 3 entries are marked as foil finish
            var foilCount = _mainVM.MyCollectionVM.Cards.Count(c => string.Equals(c.SelectedFinish, "foil", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(4, foilCount);

            // Assert mana cost images load correctly for known keys for both CardListViewModel objects
            var validManaCostKeys = new HashSet<string>
            {
                "{1}{B}",
                "{1}{G}",
                "{1}{G}{G}",
                "{1}{G}{U}",
                "{1}{R}",
                "{1}{W}",
                "{1}{W}{U}",
                "{2}",
                "{2}{B}",
                "{2}{G}",
                "{2}{G}{G}",
                "{2}{U}",
                "{2}{W}",
                "{2}{W}{W}",
                "{3}{B}",
                "{3}{G}",
                "{3}{R}",
                "{3}{U}",
                "{4}",
                "{4}{G}",
                "{4}{G}{G}",
                "{4}{R}",
                "{5}{R}",
                "{5}{W}{W}",
                "{B}",
                "{W}",
                "{X}{G}{U}",
                "{X}{W}"
            };

            foreach (var card in _mainVM.AllCardsVM.Cards)
            {
                var key = card.ManaCostRaw ?? card.ManaCost ?? string.Empty;
                if (!string.IsNullOrEmpty(key) && validManaCostKeys.Contains(key))
                {
                    var img = card.ManaCostImage; // triggers provider decode
                    if (img == null)
                    {
                        Debug.WriteLine($"Missing ManaCostImage for '{card.Name}' key '{key}'");
                    }

                    Assert.NotNull(img);
                    Assert.IsType<System.Windows.Media.ImageSource>(img, exactMatch: false);

                    // Optional: ensure thread-safety perf
                    if (img is System.Windows.Media.Imaging.BitmapImage bmp)
                    {
                        Assert.True(bmp.IsFrozen, "Bitmap should be frozen.");
                    }
                }
            }

            foreach (var card in _mainVM.MyCollectionVM.Cards)
            {
                var key = card.ManaCostRaw ?? card.ManaCost ?? string.Empty;
                if (!string.IsNullOrEmpty(key) && validManaCostKeys.Contains(key))
                {
                    var img = card.ManaCostImage; // triggers provider decode
                    if (img == null)
                    {
                        Debug.WriteLine($"Missing ManaCostImage for '{card.Name}' key '{key}'");
                    }

                    Assert.NotNull(img);
                    Assert.IsType<System.Windows.Media.ImageSource>(img, exactMatch: false);

                    // Optional: ensure thread-safety perf
                    if (img is System.Windows.Media.Imaging.BitmapImage bmp)
                    {
                        Assert.True(bmp.IsFrozen, "Bitmap should be frozen.");
                    }
                }
            }

            // Assert set icons images load correctly for known keys for both CardListViewModel objects
            var validSetCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "3ED",
                "5DN",
                "ACLB",
                "ACR",
                "AKR",
                "AMH1",
                "AMH2",
                "AMH3",
                "AMID",
                "AONE",
                "ASTX",
                "AVOW",
                "BFZ",
                "ELD",
                "FEM",
                "FJ25",
                "GRN",
                "HML",
                "ICE",
                "IMA",
                "INV",
                "J25",
                "JOU",
                "LRW",
                "ME3",
                "MH2",
                "MID",
                "MMQ",
                "OGW",
                "ONS",
                "PAVR",
                "PEMN",
                "PIO",
                "PKHM",
                "PLST",
                "PRM",
                "RAV",
                "RTR",
                "SPG",
                "THB",
                "UND",
                "USG",
                "WHO",
                "ZEN"
            };

            foreach (var card in _mainVM.AllCardsVM.Cards)
            {
                var setCode = card.Core?.SetCode ?? card.SetCode ?? string.Empty;
                if (!string.IsNullOrEmpty(setCode) && validSetCodes.Contains(setCode))
                {
                    var image = card.KeyRuneImage;
                    if (image == null)
                    {
                        Debug.WriteLine($"Missing SetIconImage for card '{card.Name}' set '{setCode}'");
                    }
                    Assert.NotNull(image);
                    Assert.IsAssignableFrom<System.Windows.Media.ImageSource>(image);

                    if (image is System.Windows.Media.Imaging.BitmapImage bmp)
                    {
                        Assert.True(bmp.IsFrozen, "Bitmap should be frozen for thread safety.");
                    }
                }
            }

            foreach (var card in _mainVM.MyCollectionVM.Cards)
            {
                var setCode = card.Core?.SetCode ?? card.SetCode ?? string.Empty;
                if (!string.IsNullOrEmpty(setCode) && validSetCodes.Contains(setCode))
                {
                    var image = card.KeyRuneImage;
                    if (image == null)
                    {
                        Debug.WriteLine($"Missing SetIconImage for card '{card.Name}' set '{setCode}'");
                    }
                    Assert.NotNull(image);
                    Assert.IsAssignableFrom<System.Windows.Media.ImageSource>(image);

                    if (image is System.Windows.Media.Imaging.BitmapImage bmp)
                    {
                        Assert.True(bmp.IsFrozen, "Bitmap should be frozen for thread safety.");
                    }
                }
            }

        }

    }
    public sealed class FilterViewModelIntegrationTests(InMemoryDatabaseFixture fx) : IClassFixture<InMemoryDatabaseFixture>, IAsyncLifetime
    {
        private IDbConnectionFactory _dbFactory = null!;
        private MainWindowViewModel _mainVM = null!;
        private readonly InMemoryDatabaseFixture _fx = fx;

        public async ValueTask InitializeAsync()
        {
            _dbFactory = SharedMemoryDbFactory.CreateInMemoryDbFactory(_fx.DbName);
            (_mainVM, _) = await TestAppBuilder.BuildAsync(_fx, _dbFactory);
        }

        public ValueTask DisposeAsync()
        {
            _mainVM.Dispose();
            return ValueTask.CompletedTask;
        }

        [Fact]
        public void FilterViewModel_Object_Creation_Initialization()
        {
            var nameFilter = _mainVM.FilterVM.Filters["Name"];
            Assert.NotEmpty(nameFilter.FilterOptions);

            Assert.True(_mainVM.FilterVM.Filters.ContainsKey("SetName"), "Expected filter key 'SetName' not found.");
            var setNameFilter = _mainVM.FilterVM.Filters["SetName"];
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
                "Bloom Tender // Bloom Tender",
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
                "Sokrates, Athenian Teacher",
            };

            // Assert that the filter options contain all expected names.
            var actualOptionNames = nameFilter.FilterOptions.Select(o => o.OptionName).ToList();

            var missingNames = expectedNames
                .Where(expected =>
                    !actualOptionNames.Any(actual => actual.Contains(expected)))
                .ToList();

            Assert.True(
                missingNames.Count == 0,
                $"Missing filter names: {string.Join(", ", missingNames)}");


            // Rarity:
            var rarityFilter = _mainVM.FilterVM.Filters["Rarity"];
            var expectedRarityOptions = new List<string> { "common", "uncommon", "rare", "mythic" };

            var actualRarityOptions = rarityFilter.FilterOptions
                .Select(opt => opt.OptionName)
                .OrderBy(x => x)
                .ToList();

            var sortedExpectedRarityOptions = expectedRarityOptions.OrderBy(x => x).ToList();
            Assert.Equal(sortedExpectedRarityOptions, actualRarityOptions);


            // Keywords:
            var keywordsFilter = _mainVM.FilterVM.Filters["Keywords"];
            var expectedKeywordsOptions = new List<string>
            {
                "Aftermath","Changeling","Converge","Defender","Devoid","Enchant","Enrage","Evoke","Fight","First strike","Flash","Flying","Haste","Ingest","Kicker","Landfall","Lifelink","Meld","Menace","Paradox","Provoke","Prowess","Radiance","Reach","Scry","Sokratic Dialogue","Team TARDIS","Vigilance"
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
            var subTypesFilter = _mainVM.FilterVM.Filters["SubTypes"];
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
            var subTypesLabelFilter = _mainVM.FilterVM.Filters["SubTypes"];
            Assert.Equal("Subtypes", subTypesLabelFilter.ReadableLabel);


            // SelectedCondition:
            var selectedConditionFilter = _mainVM.FilterVM.Filters["SelectedCondition"];
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
            var selectedFinishFilter = _mainVM.FilterVM.Filters["SelectedFinish"];
            var expectedSelectedFinishOptions = new List<string>
            {
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
            var selectedFinishLabelFilter = _mainVM.FilterVM.Filters["SelectedFinish"];
            Assert.Equal("Chosen finish", selectedFinishLabelFilter.ReadableLabel);

            // Language:
            var selectedLanguageFilter = _mainVM.FilterVM.Filters["Language"];
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
            var colorFilter = _mainVM.FilterVM.Filters["Colors"];
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
            var manaValueFilter = _mainVM.FilterVM.Filters["ManaValue"];
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
    }
    public sealed class ScenarioWithEventsIntegrationTests(InMemoryDatabaseFixture fx) : IClassFixture<InMemoryDatabaseFixture>, IAsyncLifetime
    {
        private MainWindowViewModel _mainVM = null!;
        private readonly InMemoryDatabaseFixture _fx = fx;
        private readonly FilteringService _filteringService = new();
        private IDbConnectionFactory _dbFactory = null!;

        public async ValueTask InitializeAsync()
        {
            _dbFactory = SharedMemoryDbFactory.CreateInMemoryDbFactory(_fx.DbName);
            (_mainVM, _) = await TestAppBuilder.BuildAsync(_fx, _dbFactory);
        }

        public ValueTask DisposeAsync()
        {
            _mainVM.Dispose();
            return ValueTask.CompletedTask;
        }

        [Fact]
        public async Task Filter_Integration_Test_Scenario_With_Event_Subscription()
        {
            // local helper: apply current filters to both views
            void ApplyAll()
            {
                _mainVM.AllCardsVM.FilteredCards = _filteringService.ApplyFilters(_mainVM.AllCardsVM.Cards, _mainVM.FilterVM.Filters.Values);
                _mainVM.MyCollectionVM.FilteredCards = _filteringService.ApplyFilters(_mainVM.MyCollectionVM.Cards, _mainVM.FilterVM.Filters.Values);
            }

            void AssertFiltersCleared()
            {
                Assert.Equal(65, _mainVM.AllCardsVM.FilteredCards.Count);
                Assert.Equal(22, _mainVM.MyCollectionVM.FilteredCards.Count);
                Assert.True(string.IsNullOrEmpty(_mainVM.FilterVM.FilterSummary));
            }


            // local helper: find card by uuid from either AllCards or MyCollection
            CardSet FindCard(IEnumerable<CardSet> source, string uuid) => source.Single(c => string.Equals(c.Uuid, uuid, StringComparison.OrdinalIgnoreCase));

            // ===== Section A: "Simple" test =====

            // Arrange: ManaValue > 1
            var numericFilter = _mainVM.FilterVM.Filters["ManaValue"];
            numericFilter.SelectedNumericValue = 1;
            numericFilter.OperatorSelection = OperatorType.GREATER_THAN;

            // Arrange: Rarity NOT (mythic OR rare)
            var rarityFilter = _mainVM.FilterVM.Filters["Rarity"];
            foreach (var opt in rarityFilter.FilterOptions.Where(o => o.OptionName is "mythic" or "rare"))
            {
                opt.IsSelected = true;
            }

            rarityFilter.OperatorSelection = OperatorType.NOT;

            // Act
            ApplyAll();

            // Assert
            var expectedSummary = "Rarity: {NOT mythic AND NOT rare} AND ManaValue > 1";
            Assert.Equal(expectedSummary, _mainVM.FilterVM.FilterSummary);
            Assert.Equal(23, _mainVM.AllCardsVM.FilteredCards.Count);
            Assert.Equal(17, _mainVM.MyCollectionVM.FilteredCards.Count);

            // Arrange: Colors {R OR G}
            var colorFilter = _mainVM.FilterVM.Filters["Colors"];
            foreach (var opt in colorFilter.FilterOptions.Where(o => o.OptionName is "R" or "G"))
            {
                opt.IsSelected = true;
            }

            colorFilter.OperatorSelection = OperatorType.OR;

            // Act
            ApplyAll();

            // Assert
            expectedSummary = "Colors: {R OR G} AND Rarity: {NOT mythic AND NOT rare} AND ManaValue > 1";
            Assert.Equal(expectedSummary, _mainVM.FilterVM.FilterSummary);
            Assert.Equal(13, _mainVM.AllCardsVM.FilteredCards.Count);
            Assert.Equal(10, _mainVM.MyCollectionVM.FilteredCards.Count);

            // Reset for main scenario
            _mainVM.FilterVM.ClearFiltersCommand?.Execute(null);

            // ===== Section B: text search by Name and setname =====

            // Act
            var nameFilter = _mainVM.FilterVM.Filters["Name"];
            nameFilter.SelectedSingleOption = "Ranger";

            // Assert
            var expectedNames = new List<string> { "Boundary Lands Ranger", "Ranger-Captain of Eos // Ranger-Captain of Eos" }.OrderBy(n => n).ToList();

            var actualNames = _mainVM.AllCardsVM.FilteredCards.Select(c => c.Name!).OrderBy(n => n).ToList();

            Assert.Equal(expectedNames, actualNames);
            Assert.Empty(_mainVM.MyCollectionVM.FilteredCards);
            Assert.Equal(2, _mainVM.AllCardsVM.FilteredCards.Count);

            // Act: Reset by typing empty string
            nameFilter.SelectedSingleOption = "";

            // Assert
            AssertFiltersCleared();

            // Act: type "modern horizons" into SetName free text search
            var setNameFilter = (TestableFilterItemViewModel)_mainVM.FilterVM.Filters["SetName"];
            setNameFilter.FreetextSearch = "modern horizons";
            setNameFilter.SimulateTypingComplete();

            // Assert
            Assert.Equal(9, _mainVM.AllCardsVM.FilteredCards.Count);

            // Act: Delete text to clear
            setNameFilter.FreetextSearch = "";

            // Assert
            AssertFiltersCleared();

            // Act: SetName = "Modern Horizons Art Series"
            setNameFilter.SelectedSingleOption = "Modern Horizons Art Series";

            // Assert
            Assert.Equal(3, _mainVM.AllCardsVM.FilteredCards.Count);

            _mainVM.FilterVM.ClearFiltersCommand?.Execute(null);
            AssertFiltersCleared();

            // ===== Section C: text + set filters =====

            // Arrange
            var rulesFilter = _mainVM.FilterVM.Filters["Text"];

            // Act: Text contains nonsense string
            rulesFilter.SelectedSingleOption = "asdfasdf";

            // Assert
            Assert.Equal(0, _mainVM.AllCardsVM.FilteredCards.Count);
            Assert.Equal(0, _mainVM.MyCollectionVM.FilteredCards.Count);

            // Act: Clear rules text filter by pressing escape
            rulesFilter.HandleKeyLogic(Key.Escape);

            // Assert: cleared
            AssertFiltersCleared();

            // Act: Text type in "a" 
            rulesFilter.FreetextSearch = "a";
            rulesFilter.HandleKeyLogic(Key.Enter); // skip delay, apply immediately

            // Assert
            Assert.Equal(46, _mainVM.AllCardsVM.FilteredCards.Count);
            Assert.Equal("Text: \"a\"", _mainVM.FilterVM.FilterSummary);
            Assert.Equal(21, _mainVM.MyCollectionVM.FilteredCards.Count);

            // Act: Press Backspace to remove "a"
            rulesFilter.FreetextSearch = rulesFilter.FreetextSearch[..^1];
            AssertFiltersCleared();

            // Act: Text contains “+1/+1 counter”
            rulesFilter.SelectedSingleOption = "+1/+1 counter";

            // Assert
            Assert.Equal(3, _mainVM.AllCardsVM.FilteredCards.Count);
            Assert.Equal(2, _mainVM.MyCollectionVM.FilteredCards.Count);

            // Act: SetName contains "The List"
            var setFilter = _mainVM.FilterVM.Filters["SetName"];
            setFilter.SelectedSingleOption = "The List";
            _mainVM.FilterVM.NotifyFilterChanged();

            // Assert
            Assert.Equal(2, _mainVM.AllCardsVM.FilteredCards.Count);
            Assert.Equal(2, _mainVM.MyCollectionVM.FilteredCards.Count);
            Assert.Equal("SetName: \"The List\" AND Text: \"+1/+1 counter\"", _mainVM.FilterVM.FilterSummary);

            // Reset
            _mainVM.FilterVM.ClearFiltersCommand?.Execute(null);

            AssertFiltersCleared();

            // ===== Section D: types + supertypes =====

            // Arrange: Types {Creature OR Planeswalker}
            var typesFilter = _mainVM.FilterVM.Filters["Types"];
            foreach (var opt in typesFilter.FilterOptions.Where(o => o.OptionName is "Creature" or "Planeswalker"))
            {
                opt.IsSelected = true;
            }

            typesFilter.OperatorSelection = OperatorType.OR;

            // Assert
            Assert.Equal(29, _mainVM.AllCardsVM.FilteredCards.Count);
            Assert.Equal(10, _mainVM.MyCollectionVM.FilteredCards.Count);

            // Arrange: SuperTypes {Legendary}
            var superTypesFilter = _mainVM.FilterVM.Filters["SuperTypes"];
            foreach (var opt in superTypesFilter.FilterOptions.Where(o => o.OptionName is "Legendary"))
            {
                opt.IsSelected = true;
            }

            // Assert
            Assert.Equal(6, _mainVM.AllCardsVM.FilteredCards.Count);
            Assert.Empty(_mainVM.MyCollectionVM.FilteredCards);
            Assert.Equal("SuperTypes: {Legendary} AND Types: {Creature OR Planeswalker}", _mainVM.FilterVM.FilterSummary);

            // ===== Section E: add one card (Karox) via AddSelectedCards =====

            // Arrange
            const string uuidKarox = "e4dcfe4f-8441-5eec-9f74-a7b3672e90e0";
            var karox = FindCard(_mainVM.AllCardsVM.FilteredCards, uuidKarox);

            // Act
            _mainVM.AddCardsVM.AddSelectedCardsCommand.Execute(new object[] { karox });

            // Assert: staged
            Assert.Single(_mainVM.AddCardsVM.CardsToAddOrEdit, c => c.CardToAddOrEdit.Uuid == uuidKarox);

            // Act: submit
            _mainVM.AddCardsVM.SubmitNewCardsCommand.Execute(null);

            // Assert: now in MyCollection
            Assert.Equal(23, _mainVM.MyCollectionVM.Cards.Count);

            // ===== Section F: add Sokrates with field edits =====

            // Act: filter by name "sokrates"
            nameFilter.SelectedSingleOption = "sokrates";

            // Assert
            expectedNames = [.. new[] { "Sokrates, Athenian Teacher" }.OrderBy(n => n)];
            actualNames = [.. _mainVM.AllCardsVM.FilteredCards.Select(c => c.Name!).OrderBy(n => n)];
            Assert.Equal(expectedNames, actualNames);
            Assert.Empty(_mainVM.MyCollectionVM.FilteredCards);

            // Arrange
            const string uuidSokrates = "3c389f9c-e459-5b16-87b5-d51644f05b25";
            var sokrates = FindCard(_mainVM.AllCardsVM.FilteredCards, uuidSokrates);

            // Act: stage Sokrates
            _mainVM.AddCardsVM.AddSelectedCardsCommand.Execute(new object[] { sokrates });

            // Assert: staged
            Assert.Single(_mainVM.AddCardsVM.CardsToAddOrEdit, c => c.CardToAddOrEdit.Uuid == uuidSokrates);

            // Arrange: modify before submit
            var pending = _mainVM.AddCardsVM.CardsToAddOrEdit.Single(c => c.CardToAddOrEdit.Uuid == uuidSokrates);
            pending.SelectedCondition = "Played";
            pending.CardsForTrade = 1;

            // Act: submit
            _mainVM.AddCardsVM.SubmitNewCardsCommand.Execute(null);

            // Assert: now in MyCollection with edits
            Assert.Equal(24, _mainVM.MyCollectionVM.Cards.Count);

            var sokratesInCollection = FindCard(_mainVM.MyCollectionVM.Cards, uuidSokrates);
            Assert.Equal("Played", sokratesInCollection.SelectedCondition);
            Assert.Equal(1, sokratesInCollection.CardsForTrade);

            // Assert: staging cleared
            Assert.Empty(_mainVM.AddCardsVM.CardsToAddOrEdit);

            //// Assert: filter facets include new values
            var conditionFilter = _mainVM.FilterVM.Filters["SelectedCondition"];
            Assert.Contains("Played", conditionFilter.AvailableOptions);

            var languageFilter = _mainVM.FilterVM.Filters["Language"];
            Assert.Contains("Ancient Greek", languageFilter.AvailableOptions);

            // ===== Section G: delete two specific cards (etched + German) =====

            // Arrange
            const string uuidEtched = "0add0930-720f-5bf5-bcf5-ee208eeb9040"; // Once Upon a Time (etched)
            const string uuidGerman = "5e6a3099-2597-5755-8a6f-67f1569a3b8a"; // Leave No Trace (German)

            var etchedCard = FindCard(_mainVM.MyCollectionVM.Cards, uuidEtched);
            var germanCard = FindCard(_mainVM.MyCollectionVM.Cards, uuidGerman);

            var deletionSelection = new object[] { etchedCard, germanCard };

            // Act
            _mainVM.AddCardsVM.DeleteSelectedCardsCommand.Execute(deletionSelection);

            // Wait defensively for async path to complete
            SpinWait.SpinUntil(() => !_mainVM.MyCollectionVM.Cards.Any(c => c.Uuid == uuidEtched || c.Uuid == uuidGerman), millisecondsTimeout: 1000);

            // Assert: removed from collection
            Assert.DoesNotContain(_mainVM.MyCollectionVM.Cards, c => c.Uuid == uuidEtched);
            Assert.DoesNotContain(_mainVM.MyCollectionVM.Cards, c => c.Uuid == uuidGerman);

            // Assert: facets updated (ImmediateScheduler makes this synchronous)
            var finishFilter = _mainVM.FilterVM.Filters["SelectedFinish"];
            Assert.DoesNotContain(finishFilter.AvailableOptions, s => string.Equals(s, "etched", StringComparison.OrdinalIgnoreCase));

            var langFilter = _mainVM.FilterVM.Filters["Language"];
            Assert.DoesNotContain(langFilter.AvailableOptions, s => string.Equals(s, "German", StringComparison.OrdinalIgnoreCase));

            // Assert: count back to 22
            Assert.Equal(22, _mainVM.MyCollectionVM.Cards.Count);

            //// ===== Section H: merge scenario (Hypnotic Cloud defaults) =====

            // Arrange
            const string uuidMerge = "413e11a5-35a1-51c7-928b-219b4453a094"; // Hypnotic Cloud
            var toMerge = _mainVM.AllCardsVM.Cards.Single(c => c.Uuid == uuidMerge);
            var mergeSelection = new object[] { toMerge };

            // Act
            _mainVM.AddCardsVM.SubmitNewCardsWithDefaultsCommand.Execute(mergeSelection);

            // Assert: still 22 after merge
            Assert.Equal(22, _mainVM.MyCollectionVM.Cards.Count);

            // Assert: merged survivor has the incremented total (VM + DB agree)
            const string cond = "Near Mint";
            const string lang = "English";
            const string finish = "nonfoil";

            int OwnedTotal(IEnumerable<CardSet> list) =>
                list.Where(c => c.Uuid == uuidMerge &&
                                string.Equals(c.SelectedCondition, cond, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(c.Language, lang, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(c.SelectedFinish, finish, StringComparison.OrdinalIgnoreCase))
                    .Sum(c => c.CardsOwned);

            var ownedVm = OwnedTotal(_mainVM.MyCollectionVM.Cards);

            var survivor = _mainVM.MyCollectionVM.Cards.Single(c =>
                c.Uuid == uuidMerge &&
                string.Equals(c.SelectedCondition, cond, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(c.Language, lang, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(c.SelectedFinish, finish, StringComparison.OrdinalIgnoreCase));

            Assert.Equal(ownedVm, survivor.CardsOwned);

            int sumOwnedDb = 0;
            int sumTradeDb = 0;

            await using (var uow = new UnitOfWork(_dbFactory))
            {
                await uow.BeginReadOnlyAsync();

                const string sql = """
                    SELECT SUM(cardsOwned) AS SumOwned, SUM(cardsForTrade) AS SumTrade
                    FROM myCollection
                    WHERE uuid = @uuid
                      AND condition = @cond
                      AND language = @lang
                      AND finish = @finish;
                    """;

                using (var cmd = new SQLiteCommand(sql, uow.CurrentConnection))
                {
                    cmd.Parameters.AddWithValue("@uuid", uuidMerge);
                    cmd.Parameters.AddWithValue("@cond", cond);
                    cmd.Parameters.AddWithValue("@lang", lang);
                    cmd.Parameters.AddWithValue("@finish", finish);

                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        sumOwnedDb = reader["SumOwned"] is DBNull ? 0 : Convert.ToInt32(reader["SumOwned"]);
                        sumTradeDb = reader["SumTrade"] is DBNull ? 0 : Convert.ToInt32(reader["SumTrade"]);
                    }
                }

                await uow.CommitAsync();
            }

            Assert.Equal(ownedVm, sumOwnedDb);

            // ===== Section I: Check keyword aggregation from b-side of card =====
            // Reset
            _mainVM.FilterVM.ClearFiltersCommand?.Execute(null);

            AssertFiltersCleared();

            // Arrange
            _mainVM.FilterVM.Filters["Keywords"].FilterOptions.FirstOrDefault(o => o.OptionName == "Vigilance")!.IsSelected = true;
            expectedNames = [.. new List<string> { "Bruna, the Fading Light // Brisela, Voice of Nightmares", "Gisela, the Broken Blade // Brisela, Voice of Nightmares" }.OrderBy(n => n)];
            actualNames = [.. _mainVM.AllCardsVM.FilteredCards.Select(c => c.Name!).OrderBy(n => n)];

            // Assert
            Assert.Equal(expectedNames, actualNames);
            Assert.Empty(_mainVM.MyCollectionVM.FilteredCards);
            Assert.Equal(2, _mainVM.AllCardsVM.FilteredCards.Count);

            // ===== Section J: location CRUD + assign/remove location through collection mutation flow =====

            // Arrange
            _mainVM.FilterVM.ClearFiltersCommand?.Execute(null);
            AssertFiltersCleared();

            var locationVm = _mainVM.CardLocationVM;

            // Act: create location
            locationVm.LocationName = "Scenario Test Deck";
            locationVm.SelectedLocationType = CardLocationType.Deck;
            await locationVm.SubmitActionCommand.ExecuteAsync(null);

            // Assert: location exists in utility VM
            var scenarioLocation = locationVm.Locations.Single(l => l.Name == "Scenario Test Deck");
            Assert.Equal(CardLocationType.Deck, scenarioLocation.Type);

            // Arrange: choose a stable existing collection card
            var targetCard = _mainVM.MyCollectionVM.Cards.First(c => c.CardId.HasValue);
            var targetCardId = targetCard.CardId!.Value;

            // Act: set location through existing collection mutation pipeline
            var param = new SetLocationForSelectedCardsParameter(new object[] { targetCard }, scenarioLocation.Id);

            _mainVM.MyCollectionPageVM.ModifyCollectionViewModel!.SetLocationForSelectedCardsCommand.Execute(param);

            // Assert: VM card has location
            var updatedTarget = _mainVM.MyCollectionVM.Cards.Single(c => c.CardId == targetCardId);
            Assert.Equal(scenarioLocation.Id, updatedTarget.SelectedLocationId);
            Assert.Equal("Scenario Test Deck", updatedTarget.SelectedLocationName);
            Assert.Equal("Deck: Scenario Test Deck", updatedTarget.SelectedLocationDisplayName);

            // Assert: DB card has location
            await using var locationCheckUow = new UnitOfWork(_dbFactory);
            await locationCheckUow.BeginReadOnlyAsync();

            using var locationCheckCmd = new SQLiteCommand(
                """
                    SELECT locationId
                    FROM myCollection
                    WHERE id = @id;
                    """,
                locationCheckUow.CurrentConnection);

            locationCheckCmd.Parameters.AddWithValue("@id", targetCardId);

            var locationIdObj = await locationCheckCmd.ExecuteScalarAsync();

            await locationCheckUow.CommitAsync();

            Assert.NotNull(locationIdObj);
            Assert.NotEqual(DBNull.Value, locationIdObj);
            Assert.Equal(scenarioLocation.Id, Convert.ToInt32(locationIdObj));

            // Act: delete location
            locationVm.SelectedLocations.Clear();
            locationVm.SelectedLocations.Add(scenarioLocation);

            locationVm.DeleteSelectedLocationsCommand.Execute(null); // first click activates confirmation
            locationVm.DeleteSelectedLocationsCommand.Execute(null); // second click confirms

            // Assert: location removed from utility VM
            Assert.DoesNotContain(locationVm.Locations, l => l.Id == scenarioLocation.Id);

            // Assert: VM card location is cleared after delete
            var clearedTarget = _mainVM.MyCollectionVM.Cards.Single(c => c.CardId == targetCardId);
            Assert.Null(clearedTarget.SelectedLocationId);
            Assert.Null(clearedTarget.SelectedLocationName);
            Assert.Null(clearedTarget.SelectedLocationDisplayName);

            // Assert: DB card location is cleared
            await using var clearCheckUow = new UnitOfWork(_dbFactory);
            await clearCheckUow.BeginReadOnlyAsync();

            using var clearCheckCmd = new SQLiteCommand(
                """
                    SELECT locationId
                    FROM myCollection
                    WHERE id = @id;
                    """, clearCheckUow.CurrentConnection);

            clearCheckCmd.Parameters.AddWithValue("@id", targetCardId);

            var clearedLocationObj = await clearCheckCmd.ExecuteScalarAsync();

            await clearCheckUow.CommitAsync();

            Assert.True(clearedLocationObj is null or DBNull);

            // ===== Section K: add multiple otters with different location/comment identities =====

            // Act: create scenario locations
            locationVm.LocationName = "Box 1";
            locationVm.SelectedLocationType = CardLocationType.Storage;
            await locationVm.SubmitActionCommand.ExecuteAsync(null);

            locationVm.LocationName = "Box 2";
            locationVm.SelectedLocationType = CardLocationType.Storage;
            await locationVm.SubmitActionCommand.ExecuteAsync(null);

            locationVm.LocationName = "Deck Awesome!";
            locationVm.SelectedLocationType = CardLocationType.Deck;
            await locationVm.SubmitActionCommand.ExecuteAsync(null);

            // Assert: locations exist
            var box1 = locationVm.Locations.Single(l => l.Name == "Box 1");
            var box2 = locationVm.Locations.Single(l => l.Name == "Box 2");
            var deckAwesome = locationVm.Locations.Single(l => l.Name == "Deck Awesome!");

            Assert.Equal(CardLocationType.Storage, box1.Type);
            Assert.Equal(CardLocationType.Storage, box2.Type);
            Assert.Equal(CardLocationType.Deck, deckAwesome.Type);

            // Arrange: add five otters with different collection identities
            const string uuidOtter = "49481296-5e87-500b-9d95-8011f432466a";
            var otter = FindCard(_mainVM.AllCardsVM.Cards, uuidOtter);

            _mainVM.AddCardsVM.AddSelectedCardsCommand.Execute(new object[] { otter, otter, otter, otter, otter });

            var pendingOtters = _mainVM.AddCardsVM.CardsToAddOrEdit
                .Where(r => r.CardToAddOrEdit.Uuid == uuidOtter)
                .ToList();

            Assert.Equal(5, pendingOtters.Count);

            // Otter 1: Box 1
            pendingOtters[0].SelectedLocationId = box1.Id;

            // Otter 2: Box 2
            pendingOtters[1].SelectedLocationId = box2.Id;

            // Otter 3: Box 2 + comment
            pendingOtters[2].SelectedLocationId = box2.Id;
            pendingOtters[2].Comment = "smudgemark";

            // Otter 4: Deck Awesome!
            pendingOtters[3].SelectedLocationId = deckAwesome.Id;

            // Otter 5: no location, no comment
            pendingOtters[4].SelectedLocationId = null;
            pendingOtters[4].Comment = null;

            // Act: submit otters
            _mainVM.AddCardsVM.SubmitNewCardsCommand.Execute(null);

            // Assert: five distinct otter rows were added
            Assert.Equal(27, _mainVM.MyCollectionVM.Cards.Count);

            var ottersInCollection = _mainVM.MyCollectionVM.Cards.Where(c => c.Uuid == uuidOtter).ToList();

            Assert.Equal(5, ottersInCollection.Count);

            Assert.Contains(ottersInCollection, c => c.SelectedLocationId == box1.Id && string.IsNullOrWhiteSpace(c.Comment));
            Assert.Contains(ottersInCollection, c => c.SelectedLocationId == box2.Id && string.IsNullOrWhiteSpace(c.Comment));
            Assert.Contains(ottersInCollection, c => c.SelectedLocationId == box2.Id && c.Comment == "smudgemark");
            Assert.Contains(ottersInCollection, c => c.SelectedLocationId == deckAwesome.Id && string.IsNullOrWhiteSpace(c.Comment));
            Assert.Contains(ottersInCollection, c => c.SelectedLocationId is null && string.IsNullOrWhiteSpace(c.Comment));

            // ===== Section L: edit otter location to none and merge with existing no-location otter =====

            // Arrange: find Otter 1 with Box 1 and the existing no-location/no-comment otter
            var otterBox1 = _mainVM.MyCollectionVM.Cards.Single(c => c.Uuid == uuidOtter && c.SelectedLocationId == box1.Id && string.IsNullOrWhiteSpace(c.Comment));

            var otterNoLocationBefore = _mainVM.MyCollectionVM.Cards.Single(c => c.Uuid == uuidOtter && c.SelectedLocationId is null && string.IsNullOrWhiteSpace(c.Comment));

            var otterBox1Id = otterBox1.CardId!.Value;
            var otterNoLocationId = otterNoLocationBefore.CardId!.Value;
            var expectedMergedOwned = otterBox1.CardsOwned + otterNoLocationBefore.CardsOwned;
            var expectedMergedTrade = otterBox1.CardsForTrade + otterNoLocationBefore.CardsForTrade;

            // Act: stage Otter 1 for edit
            _mainVM.MyCollectionPageVM.ModifyCollectionViewModel!.EditSelectedCardsCommand.Execute(new object[] { otterBox1 });

            var pendingOtterEdit = _mainVM.MyCollectionPageVM.ModifyCollectionViewModel!.CardsToAddOrEdit.Single(r => r.CardToAddOrEdit.CardId == otterBox1Id);

            // Act: clear location in edit row
            pendingOtterEdit.SelectedLocationId = null;

            // Act: submit edit
            _mainVM.MyCollectionPageVM.ModifyCollectionViewModel!.SubmitCardEditsCommand.Execute(null);

            // Assert: collection row count decreased by one due to merge
            Assert.Equal(26, _mainVM.MyCollectionVM.Cards.Count);

            // Assert: Box 1 otter row was removed
            Assert.DoesNotContain(_mainVM.MyCollectionVM.Cards, c => c.CardId == otterBox1Id);

            // Assert: no-location otter survivor remains and has merged quantities
            var otterNoLocationAfter = _mainVM.MyCollectionVM.Cards.Single(c => c.CardId == otterNoLocationId);

            Assert.Equal(uuidOtter, otterNoLocationAfter.Uuid);
            Assert.Null(otterNoLocationAfter.SelectedLocationId);
            Assert.True(string.IsNullOrWhiteSpace(otterNoLocationAfter.Comment));
            Assert.Equal(expectedMergedOwned, otterNoLocationAfter.CardsOwned);
            Assert.Equal(expectedMergedTrade, otterNoLocationAfter.CardsForTrade);

            // Refresh otters list
            ottersInCollection = [.. _mainVM.MyCollectionVM.Cards.Where(c => c.Uuid == uuidOtter)];
            Assert.Equal(4, ottersInCollection.Count); // One has been merged away, so now 4 distinct otter rows instead of 5

            Assert.Contains(ottersInCollection, c => c.SelectedLocationId == box2.Id && string.IsNullOrWhiteSpace(c.Comment));
            Assert.Contains(ottersInCollection, c => c.SelectedLocationId == box2.Id && c.Comment == "smudgemark");
            Assert.Contains(ottersInCollection, c => c.SelectedLocationId == deckAwesome.Id && string.IsNullOrWhiteSpace(c.Comment));
            Assert.Contains(ottersInCollection, c => c.SelectedLocationId is null && string.IsNullOrWhiteSpace(c.Comment) && c.CardsOwned == 2);

            // ===== Section M: edit two Box 2 otters into same new Box 1 identity =====

            // Arrange: find the two Box 2 otters
            var otterBox2NoComment = _mainVM.MyCollectionVM.Cards.Single(c => c.Uuid == uuidOtter && c.SelectedLocationId == box2.Id && string.IsNullOrWhiteSpace(c.Comment));

            var otterBox2Smudge = _mainVM.MyCollectionVM.Cards.Single(c => c.Uuid == uuidOtter && c.SelectedLocationId == box2.Id && c.Comment == "smudgemark");

            var otterBox2NoCommentId = otterBox2NoComment.CardId!.Value;
            var otterBox2SmudgeId = otterBox2Smudge.CardId!.Value;

            var expectedBox1Owned = otterBox2NoComment.CardsOwned + otterBox2Smudge.CardsOwned;
            var expectedBox1Trade = otterBox2NoComment.CardsForTrade + otterBox2Smudge.CardsForTrade;

            // Act: stage both Box 2 otters for edit
            _mainVM.MyCollectionPageVM.ModifyCollectionViewModel!.EditSelectedCardsCommand.Execute(new object[] { otterBox2NoComment, otterBox2Smudge });

            var pendingBox2Edits = _mainVM.MyCollectionPageVM.ModifyCollectionViewModel!.CardsToAddOrEdit.Where(r => r.CardToAddOrEdit.CardId == otterBox2NoCommentId || r.CardToAddOrEdit.CardId == otterBox2SmudgeId).ToList();

            Assert.Equal(2, pendingBox2Edits.Count);

            // Act: change both to Box 1 and no comment
            foreach (var pendingEdit in pendingBox2Edits)
            {
                pendingEdit.SelectedLocationId = box1.Id;
                pendingEdit.Comment = null;
            }

            // Act: submit edits
            _mainVM.MyCollectionPageVM.ModifyCollectionViewModel!.SubmitCardEditsCommand.Execute(null);

            // Assert: collection count decreased by one due to merge
            Assert.Equal(25, _mainVM.MyCollectionVM.Cards.Count);

            // Refresh otters list
            ottersInCollection = [.. _mainVM.MyCollectionVM.Cards.Where(c => c.Uuid == uuidOtter)];
            Assert.Equal(3, ottersInCollection.Count);

            // Assert: one Box 1/no-comment otter identity remains with merged quantities
            var otterBox1Merged = ottersInCollection.Single(c => c.SelectedLocationId == box1.Id && string.IsNullOrWhiteSpace(c.Comment));

            Assert.Equal(expectedBox1Owned, otterBox1Merged.CardsOwned);
            Assert.Equal(expectedBox1Trade, otterBox1Merged.CardsForTrade);

            // Assert: old Box 2 identities are gone
            Assert.DoesNotContain(ottersInCollection, c => c.SelectedLocationId == box2.Id && string.IsNullOrWhiteSpace(c.Comment));
            Assert.DoesNotContain(ottersInCollection, c => c.SelectedLocationId == box2.Id && c.Comment == "smudgemark");

            // Assert: remaining otter identities are the expected ones
            Assert.Contains(ottersInCollection, c => c.SelectedLocationId == box1.Id && string.IsNullOrWhiteSpace(c.Comment) && c.CardsOwned == 2);
            Assert.Contains(ottersInCollection, c => c.SelectedLocationId == deckAwesome.Id && string.IsNullOrWhiteSpace(c.Comment));
            Assert.Contains(ottersInCollection, c => c.SelectedLocationId is null && string.IsNullOrWhiteSpace(c.Comment) && c.CardsOwned == 2);

            // Assert: DB matches VM truth for otter rows
            await using var otterDbCheckUow = new UnitOfWork(_dbFactory);
            await otterDbCheckUow.BeginReadOnlyAsync();

            using var otterDbCheckCmd = new SQLiteCommand(
                """
                    SELECT id, uuid, locationId, comment, cardsOwned, cardsForTrade
                    FROM myCollection
                    WHERE uuid = @uuid
                    ORDER BY id;
                    """,
                otterDbCheckUow.CurrentConnection);

            otterDbCheckCmd.Parameters.AddWithValue("@uuid", uuidOtter);

            var dbRows = new List<(int Id, int? LocationId, string? Comment, int Owned, int Trade)>();

            using (var reader = await otterDbCheckCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var locationOrdinal = reader.GetOrdinal("locationId");
                    var commentOrdinal = reader.GetOrdinal("comment");

                    dbRows.Add((
                        Id: reader.GetInt32(reader.GetOrdinal("id")),
                        LocationId: reader.IsDBNull(locationOrdinal)
                            ? null
                            : reader.GetInt32(locationOrdinal),
                        Comment: reader.IsDBNull(commentOrdinal)
                            ? null
                            : reader.GetString(commentOrdinal),
                        Owned: reader.GetInt32(reader.GetOrdinal("cardsOwned")),
                        Trade: reader.GetInt32(reader.GetOrdinal("cardsForTrade"))
                    ));
                }
            }

            await otterDbCheckUow.CommitAsync();

            Assert.Equal(3, dbRows.Count);
            Assert.Contains(dbRows, r => r.LocationId == box1.Id && string.IsNullOrWhiteSpace(r.Comment) && r.Owned == expectedBox1Owned && r.Trade == expectedBox1Trade);
            Assert.Contains(dbRows, r => r.LocationId == deckAwesome.Id && string.IsNullOrWhiteSpace(r.Comment));
            Assert.Contains(dbRows, r => r.LocationId is null && string.IsNullOrWhiteSpace(r.Comment) && r.Owned == 2);
            Assert.DoesNotContain(dbRows, r => r.LocationId == box2.Id);

            // ===== Section N: deleting location merges staged row and reconciles edit list =====

            // Arrange: stage Deck Awesome otter for edit
            var otterDeckAwesome = _mainVM.MyCollectionVM.Cards.Single(c => c.Uuid == uuidOtter && c.SelectedLocationId == deckAwesome.Id && string.IsNullOrWhiteSpace(c.Comment));

            var otterDeckAwesomeId = otterDeckAwesome.CardId!.Value;

            var otterNoLocationBeforeDelete = _mainVM.MyCollectionVM.Cards.Single(c => c.Uuid == uuidOtter && c.SelectedLocationId is null && string.IsNullOrWhiteSpace(c.Comment));

            otterNoLocationId = otterNoLocationBeforeDelete.CardId!.Value;
            var expectedNoLocationOwnedAfterDelete = otterNoLocationBeforeDelete.CardsOwned + otterDeckAwesome.CardsOwned;
            var expectedNoLocationTradeAfterDelete = otterNoLocationBeforeDelete.CardsForTrade + otterDeckAwesome.CardsForTrade;

            _mainVM.MyCollectionPageVM.ModifyCollectionViewModel!.EditSelectedCardsCommand.Execute(new object[] { otterDeckAwesome });

            // Assert: staged before location delete
            Assert.Contains(_mainVM.MyCollectionPageVM.ModifyCollectionViewModel!.CardsToAddOrEdit, r => r.CardToAddOrEdit.CardId == otterDeckAwesomeId);

            // Act: delete Deck Awesome location
            locationVm.SelectedLocations.Clear();
            locationVm.SelectedLocations.Add(deckAwesome);

            await locationVm.DeleteSelectedLocationsCommand.ExecuteAsync(null); // activate confirmation
            await locationVm.DeleteSelectedLocationsCommand.ExecuteAsync(null); // confirm delete

            // Assert: staged Deck Awesome otter was removed because its source row was merged away
            Assert.DoesNotContain(_mainVM.MyCollectionPageVM.ModifyCollectionViewModel!.CardsToAddOrEdit, r => r.CardToAddOrEdit.CardId == otterDeckAwesomeId);

            // Assert: collection count decreased by one due to merge
            Assert.Equal(24, _mainVM.MyCollectionVM.Cards.Count);

            // Refresh otters list
            ottersInCollection = [.. _mainVM.MyCollectionVM.Cards.Where(c => c.Uuid == uuidOtter)];

            // Assert: now only two otter collection rows remain
            Assert.Equal(2, ottersInCollection.Count);

            // Assert: Deck Awesome otter row was removed
            Assert.DoesNotContain(ottersInCollection, c => c.CardId == otterDeckAwesomeId);
            Assert.DoesNotContain(ottersInCollection, c => c.SelectedLocationId == deckAwesome.Id);

            // Assert: no-location/no-comment survivor absorbed Deck Awesome otter
            var otterNoLocationAfterDelete = ottersInCollection.Single(c => c.CardId == otterNoLocationId);

            Assert.Null(otterNoLocationAfterDelete.SelectedLocationId);
            Assert.True(string.IsNullOrWhiteSpace(otterNoLocationAfterDelete.Comment));
            Assert.Equal(expectedNoLocationOwnedAfterDelete, otterNoLocationAfterDelete.CardsOwned);
            Assert.Equal(expectedNoLocationTradeAfterDelete, otterNoLocationAfterDelete.CardsForTrade);

            // Assert: Box 1 otter still exists
            Assert.Contains(ottersInCollection, c => c.SelectedLocationId == box1.Id && string.IsNullOrWhiteSpace(c.Comment) && c.CardsOwned == 2);
            Assert.Contains(ottersInCollection, c => c.SelectedLocationId is null && string.IsNullOrWhiteSpace(c.Comment) && c.CardsOwned == 3);

            // ===== Section O: simulated right-click set location merges remaining otters into Deck Awesome identity =====

            // Arrange: recreate Deck Awesome because it was deleted in previous section
            locationVm.LocationName = "Deck Awesome!";
            locationVm.SelectedLocationType = CardLocationType.Deck;
            await locationVm.SubmitActionCommand.ExecuteAsync(null);

            deckAwesome = locationVm.Locations.Single(l => l.Name == "Deck Awesome!");

            ottersInCollection = [.. _mainVM.MyCollectionVM.Cards.Where(c => c.Uuid == uuidOtter)];

            Assert.Equal(2, ottersInCollection.Count);

            var expectedDeckOwned = ottersInCollection.Sum(c => c.CardsOwned);
            var expectedDeckTrade = ottersInCollection.Sum(c => c.CardsForTrade);

            // Act: simulate right-click command on the two remaining otters
            var setDeckParam = new SetLocationForSelectedCardsParameter(ottersInCollection.Cast<object>().ToArray(), deckAwesome.Id);

            _mainVM.MyCollectionPageVM.ModifyCollectionViewModel!.SetLocationForSelectedCardsCommand.Execute(setDeckParam);

            // Assert: collection count decreased by one due to merge
            Assert.Equal(23, _mainVM.MyCollectionVM.Cards.Count);

            // Assert: all otters merged into one Deck Awesome identity
            ottersInCollection = [.. _mainVM.MyCollectionVM.Cards.Where(c => c.Uuid == uuidOtter)];

            var finalOtter = Assert.Single(ottersInCollection);

            Assert.Equal(deckAwesome.Id, finalOtter.SelectedLocationId);
            Assert.True(string.IsNullOrWhiteSpace(finalOtter.Comment));
            Assert.Equal(5, finalOtter.CardsOwned);
            Assert.Equal(expectedDeckOwned, finalOtter.CardsOwned);
            Assert.Equal(expectedDeckTrade, finalOtter.CardsForTrade);
            Assert.Equal("Deck: Deck Awesome!", finalOtter.SelectedLocationDisplayName);

            // Assert: DB truth matches VM after right-click location merge
            await using (var finalOtterDbCheckUow = new UnitOfWork(_dbFactory))
            {
                await finalOtterDbCheckUow.BeginReadOnlyAsync();

                using var cmd = new SQLiteCommand(
                    """
                    SELECT id, locationId, comment, cardsOwned, cardsForTrade
                    FROM myCollection
                    WHERE uuid = @uuid;
                    """,
                    finalOtterDbCheckUow.CurrentConnection);

                cmd.Parameters.AddWithValue("@uuid", uuidOtter);

                dbRows = [];

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var locationOrdinal = reader.GetOrdinal("locationId");
                        var commentOrdinal = reader.GetOrdinal("comment");

                        dbRows.Add((
                            Id: reader.GetInt32(reader.GetOrdinal("id")),
                            LocationId: reader.IsDBNull(locationOrdinal)
                                ? null
                                : reader.GetInt32(locationOrdinal),
                            Comment: reader.IsDBNull(commentOrdinal)
                                ? null
                                : reader.GetString(commentOrdinal),
                            Owned: reader.GetInt32(reader.GetOrdinal("cardsOwned")),
                            Trade: reader.GetInt32(reader.GetOrdinal("cardsForTrade"))
                        ));
                    }
                }

                await finalOtterDbCheckUow.CommitAsync();

                var (Id, LocationId, Comment, Owned, Trade) = Assert.Single(dbRows);

                Assert.Equal(deckAwesome.Id, LocationId);
                Assert.True(string.IsNullOrWhiteSpace(Comment));
                Assert.Equal(5, Owned);
                Assert.Equal(expectedDeckTrade, Trade);
            }
        }
    }
}
