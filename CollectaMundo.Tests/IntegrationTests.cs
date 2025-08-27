using CollectaMundo.ApplicationServices;
using CollectaMundo.ApplicationServices.CardDatabaseManagement;
using CollectaMundo.ApplicationServices.CardIcons;
using CollectaMundo.ApplicationServices.CardLists;
using CollectaMundo.ApplicationServices.CardPrices;
using CollectaMundo.ApplicationServices.DownloadResourceFiles;
using CollectaMundo.ApplicationServices.EditCollection;
using CollectaMundo.ApplicationServices.Filtering;
using CollectaMundo.ApplicationServices.GenerateMissingPng;
using CollectaMundo.ApplicationServices.ImportExport;
using CollectaMundo.ApplicationServices.Utilities.Progress;
using CollectaMundo.Data.CardDatabaseManagement;
using CollectaMundo.Data.CardIcons;
using CollectaMundo.Data.CardLists;
using CollectaMundo.Data.CardPrices;
using CollectaMundo.Data.EditCollection;
using CollectaMundo.Data.Filtering;
using CollectaMundo.Data.GenerateMissingPng;
using CollectaMundo.Data.ImportExport;
using CollectaMundo.Data.RemoteLookups;
using CollectaMundo.DomainLogic.EditCollection;
using CollectaMundo.DomainLogic.EditCollection.Models;
using CollectaMundo.DomainLogic.Filtering;
using CollectaMundo.DomainLogic.GenerateMissingPng;
using CollectaMundo.ViewModels;
using System.Diagnostics;
using System.Windows;

namespace CollectaMundo.Tests
{

    public sealed class IntegrationTests(InMemoryDatabaseFixture fixture)
        : IClassFixture<InMemoryDatabaseFixture>, IAsyncLifetime
    {
        private readonly InMemoryDatabaseFixture _fx = fixture;
        private MainWindowViewModel _mainVM = null!;
        private readonly List<CardChangeEventArgs> _changedEvents = [];
        private readonly FilteringService _filteringService = new();

        // ---- IAsyncLifetime ----
        public async Task InitializeAsync()
        {
            // 1) Point the app at THIS fixture’s in-memory DB instance
            var dbFactory = TestUtilities.CreateInMemoryDbFactory(_fx.DbName);
            AppGlobals.DbFactory = dbFactory;

            // 2) Status overlay (same object the app would use)
            var statusVM = new StatusViewModel();

            // 3) Build the merged/updated stack (mirrors BuildAndStartAsync, minus integrity/FTS)
            var settings = new JsonAppSettings();
            var remoteLookups = new RemoteLookups();
            var downloadService = new DownloadService();

            var missingPngRepo = new GenerateMissingPngRepository();
            var missingPngLogic = new GenerateMissingPngLogic();
            var missingPngSvc = new GenerateMissingPngService(missingPngRepo, remoteLookups, missingPngLogic);

            var cardPriceRepo = new CardPriceRepository();
            var priceService = new CardPriceService(settings, cardPriceRepo);

            var prepRepo = new CardDatabasePreparationRepo();
            var progressSinks = CreateProgressSinks(statusVM); // <- local helper (below)

            var cardIconsRepo = new CardIconsRepo();
            var cardIconService = new CardIconsService(cardIconsRepo);

            var cardListRepo = new CardListRepository();
            var filterDefaultsRepo = new FilterDefaultsLogic();
            var cardListService = new CardListService(cardListRepo, filterDefaultsRepo, cardIconService);

            // IMPORTANT: inject the fixture-backed DbFactory so all DB calls stay in-memory
            var prepService = new CardDatabasePreparationService(settings, AppGlobals.DbFactory!, progressSinks, prepRepo, priceService, missingPngSvc, downloadService, remoteLookups);

            // 4) Feature-layer services
            var editCollectionRepo = new EditCollectionRepository();
            var editService = new EditCollectionService((new EditCollectionLogic(editCollectionRepo)));
            var importExportService = new ImportExportService(new ImportExportRepo());

            // 5) Build the Main VM (same signature as in BuildAndStartAsync)
            _mainVM = await MainWindowViewModel.CreateAsync(_filteringService, editService, importExportService, prepService, downloadService, statusVM, cardListService);

            // 6) Bring the VM to a “ready” state consistent with the app
            _mainVM.FilterVM.NotifyFilterChanged();
            _mainVM.SideMenuVisibility = Visibility.Visible;
            _mainVM.ContentSectionVisibility = Visibility.Visible;
            _mainVM.MainGridVisibility = Visibility.Visible;

            // Optional readiness spin (tiny) in case CreateAsync schedules initial loads
            SpinWait.SpinUntil(
                () => _mainVM.AllCardsVM.Cards.Count >= 61 && _mainVM.MyCollectionVM.Cards.Count >= 22,
                millisecondsTimeout: 500);

            // 7) Hook events for the scenario test
            _mainVM.AddCardsVM.CardChanged += (_, e) => _changedEvents.Add(e);
            _mainVM.EditCardsVM.CardChanged += (_, e) => _changedEvents.Add(e);
        }

        // Local test helper mirroring your StartupComposition helper
        private static ProgressSinks CreateProgressSinks(StatusViewModel vm) => new()
        {
            Headline = new Progress<string>(s => vm.StatusLabel1 = s),
            Detail = new Progress<string>(s => vm.StatusLabel2 = s),
            Step = new Progress<string>(s => vm.StatusLabel3 = s),
            Percent = new Progress<int>(p => vm.ProgressValue = p),
            ProgressBarVisible = new Progress<bool>(v =>
                vm.ProgressVisibility = v ? Visibility.Visible : Visibility.Collapsed)
        };
        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public void Seed_has_expected_counts()
        {
            var allCards = _mainVM.AllCardsVM.Cards;
            var myCollection = _mainVM.MyCollectionVM.Cards;

            Assert.Equal(61, allCards.Count);
            Assert.Equal(22, myCollection.Count);
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

            var actualAllCardsNames = _mainVM.AllCardsVM.Cards.Select(card => card.Name ?? string.Empty).OrderBy(name => name).ToList();
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
            Assert.Equal(3, foilCount);

            // Assert mana cost images load correctly for known keys for both CardViewModel objects
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
                    if (img == null) Debug.WriteLine($"Missing ManaCostImage for '{card.Name}' key '{key}'");
                    Assert.NotNull(img);
                    Assert.IsType<System.Windows.Media.ImageSource>(img, exactMatch: false);

                    // Optional: ensure thread-safety perf
                    if (img is System.Windows.Media.Imaging.BitmapImage bmp)
                        Assert.True(bmp.IsFrozen, "Bitmap should be frozen.");
                }
            }

            foreach (var card in _mainVM.MyCollectionVM.Cards)
            {
                var key = card.ManaCostRaw ?? card.ManaCost ?? string.Empty;
                if (!string.IsNullOrEmpty(key) && validManaCostKeys.Contains(key))
                {
                    var img = card.ManaCostImage; // triggers provider decode
                    if (img == null) Debug.WriteLine($"Missing ManaCostImage for '{card.Name}' key '{key}'");
                    Assert.NotNull(img);
                    Assert.IsType<System.Windows.Media.ImageSource>(img, exactMatch: false);

                    // Optional: ensure thread-safety perf
                    if (img is System.Windows.Media.Imaging.BitmapImage bmp)
                        Assert.True(bmp.IsFrozen, "Bitmap should be frozen.");
                }
            }

            // Assert set icons images load correctly for known keys for both CardViewModel objects
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
            Assert.True(expectedNames.All(expected => nameFilter.FilterOptions.Any(opt => opt.OptionName.Contains(expected))),
                "Not all expected filter names were found.");

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

        [Fact]
        public void Filter_Integration_Test_Simple()
        {
            // Arrange: Filter on ManaValue > 1.
            var numericFilter = _mainVM.FilterVM.Filters["ManaValue"];
            numericFilter.SelectedNumericValue = 1;
            numericFilter.OperatorSelection = OperatorType.GREATER_THAN;

            // Filter on Rarity not being mythic or rare.
            var rarityFilter = _mainVM.FilterVM.Filters["Rarity"];
            foreach (var opt in rarityFilter.FilterOptions.Where(o => o.OptionName is "mythic" or "rare"))
            {
                opt.IsSelected = true;          // this setter calls NotifyFilterChanged
            }
            rarityFilter.OperatorSelection = OperatorType.NOT;

            // Act: Apply filtering to TestAllCardsVM and TestMyCollectionVM.
            _mainVM.AllCardsVM.FilteredCards = _filteringService.ApplyFilters(_mainVM.AllCardsVM.Cards, _mainVM.FilterVM.Filters.Values);
            var filteredAllCards = _mainVM.AllCardsVM.FilteredCards;

            _mainVM.MyCollectionVM.FilteredCards = _filteringService.ApplyFilters(_mainVM.MyCollectionVM.Cards, _mainVM.FilterVM.Filters.Values);
            var filteredMyCollection = _mainVM.MyCollectionVM.FilteredCards;

            // Assert: Expected summary string
            string expectedSummary = "Rarity: {NOT mythic AND NOT rare} AND ManaValue > 1";
            Assert.Equal(expectedSummary, _mainVM.FilterVM.FilterSummary);

            // Assert: Number of cards in filteredAllCards and filteredMyCollection.
            Assert.Equal(22, filteredAllCards.Count);
            Assert.Equal(17, filteredMyCollection.Count);

            // Arrange: Add color filters to existing filters.
            var colorFilter = _mainVM.FilterVM.Filters["Colors"];
            foreach (var opt in colorFilter.FilterOptions.Where(o => o.OptionName is "R" or "G"))
            {
                opt.IsSelected = true;          // this setter calls NotifyFilterChanged
            }
            colorFilter.OperatorSelection = OperatorType.OR;

            // Act: Apply filtering to TestAllCardsVM and TestMyCollectionVM.
            _mainVM.AllCardsVM.FilteredCards = _filteringService.ApplyFilters(_mainVM.AllCardsVM.Cards, _mainVM.FilterVM.Filters.Values);
            filteredAllCards = _mainVM.AllCardsVM.FilteredCards;

            _mainVM.MyCollectionVM.FilteredCards = _filteringService.ApplyFilters(_mainVM.MyCollectionVM.Cards, _mainVM.FilterVM.Filters.Values);
            filteredMyCollection = _mainVM.MyCollectionVM.FilteredCards;

            // Assert: Expected summary string
            expectedSummary = "Colors: {R OR G} AND Rarity: {NOT mythic AND NOT rare} AND ManaValue > 1";
            Assert.Equal(expectedSummary, _mainVM.FilterVM.FilterSummary);

            // Assert: Number of cards in filteredAllCards and filteredMyCollection.
            Assert.Equal(12, filteredAllCards.Count);
            Assert.Equal(10, filteredMyCollection.Count);
        }

        [Fact]
        public void Filter_Integration_Test_Scenario_With_Event_Subscription()
        {

            // Act: Apply first filter – Name contains "Ranger"
            var nameFilter = _mainVM.FilterVM.Filters["Name"];
            nameFilter.SelectedSingleOption = "Ranger";

            // Assert: only the two “Ranger” cards appear, none in MyCollection
            var expectedNames = new List<string> { "Boundary Lands Ranger", "Ranger-Captain of Eos // Ranger-Captain of Eos" }.OrderBy(n => n).ToList();

            var actualNames = _mainVM.AllCardsVM.FilteredCards
                               .Select(c => c.Name!)   // names are non‑null in seed data
                               .OrderBy(n => n)
                               .ToList();

            Assert.Equal(expectedNames, actualNames);
            Assert.Empty(_mainVM.MyCollectionVM.FilteredCards);

            // Arrange: Clear all filters via the command 
            _mainVM.FilterVM.ClearFiltersCommand?.Execute(null);   // raises FilterChanged again

            // Assert: lists are back to their full size and summary text is empty
            Assert.Equal(61, _mainVM.AllCardsVM.FilteredCards.Count);
            Assert.Equal(22, _mainVM.MyCollectionVM.FilteredCards.Count);
            Assert.True(string.IsNullOrEmpty(_mainVM.FilterVM.FilterSummary));

            // Act: Start over with filtering – filter on rules text
            var rulesFilter = _mainVM.FilterVM.Filters["Text"];
            rulesFilter.SelectedSingleOption = "+1/+1 counter";

            // Assert: three cards in AllCards, two in MyCollection with +1/+1 counter in their rules text
            Assert.Equal(3, _mainVM.AllCardsVM.FilteredCards.Count);
            Assert.Equal(2, _mainVM.MyCollectionVM.FilteredCards.Count);

            // Act: Add one more filter - SetName contains "The List"
            var setFilter = _mainVM.FilterVM.Filters["SetName"];
            setFilter.SelectedSingleOption = "The List";
            _mainVM.FilterVM.NotifyFilterChanged();

            // Assert: two cards in AllCards, two in MyCollection with +1/+1 counter in their rules text and from the set "The List"
            Assert.Equal(2, _mainVM.AllCardsVM.FilteredCards.Count);
            Assert.Equal(2, _mainVM.MyCollectionVM.FilteredCards.Count);
            Assert.Equal("SetName: \"The List\" AND Text: \"+1/+1 counter\"", _mainVM.FilterVM.FilterSummary);


            // Arrange: Clear all filters via the command for the second time
            _mainVM.FilterVM.ClearFiltersCommand?.Execute(null);   // raises FilterChanged again

            // Assert: lists are back to their full size and summary text is empty
            Assert.Equal(61, _mainVM.AllCardsVM.FilteredCards.Count);
            Assert.Equal(22, _mainVM.MyCollectionVM.FilteredCards.Count);
            Assert.True(string.IsNullOrEmpty(_mainVM.FilterVM.FilterSummary));

            // Act: Add a filter on the "Types" filter - select "Creature" and "Planeswalker"
            var typesFilter = _mainVM.FilterVM.Filters["Types"];
            foreach (var opt in typesFilter.FilterOptions.Where(o => o.OptionName is "Creature" or "Planeswalker"))
            {
                opt.IsSelected = true;
            }
            typesFilter.OperatorSelection = OperatorType.OR;

            // Assert: 25 cards in AllCards, 10 in MyCollection with type "Creature" or "Planeswalker"
            Assert.Equal(27, _mainVM.AllCardsVM.FilteredCards.Count);
            Assert.Equal(10, _mainVM.MyCollectionVM.FilteredCards.Count);

            // Act: Add a filter on the "SuperTypes" filter - select "Legendary"
            var superTypesFilter = _mainVM.FilterVM.Filters["SuperTypes"];
            foreach (var opt in superTypesFilter.FilterOptions.Where(o => o.OptionName is "Legendary"))
            {
                opt.IsSelected = true;
            }

            // Assert: 3 cards in AllCards, none in MyCollection with type "Creature" or "Planeswalker" and supertype "Legendary"
            Assert.Equal(5, _mainVM.AllCardsVM.FilteredCards.Count);
            Assert.Empty(_mainVM.MyCollectionVM.FilteredCards);
            Assert.Equal("SuperTypes: {Legendary} AND Types: {Creature OR Planeswalker}", _mainVM.FilterVM.FilterSummary);


            // 1) pick the one FilteredCards item you want
            var uuidToAdd = "e4dcfe4f-8441-5eec-9f74-a7b3672e90e0";
            var cardToAdd = _mainVM.AllCardsVM.FilteredCards.Single(c => c.Uuid == uuidToAdd);

            // 2) “fake” the DataGrid selection by wrapping it in an object‐array
            var selection = new object[] { cardToAdd };

            // 3) call the command exactly as the UI would
            _mainVM.AddCardsVM.AddSelectedCardsCommand.Execute(selection);

            // at that point addVM.CardsToAdd contains your card
            Assert.Single(_mainVM.AddCardsVM.CardsToAdd, c => c.Uuid == uuidToAdd);

            _mainVM.AddCardsVM.SubmitNewCardsCommand.Execute(null);
            Assert.Equal(23, _mainVM.MyCollectionVM.Cards.Count);
        }
    }
}




