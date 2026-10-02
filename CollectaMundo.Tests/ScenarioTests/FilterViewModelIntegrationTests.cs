using CollectaMundo.DomainLogic.Filtering.Enums;
using CollectaMundo.Tests.TestUtils;

namespace CollectaMundo.Tests.ScenarioTests
{
    public sealed class FilterViewModelIntegrationTests(InMemoryDatabaseFixture fx) : IClassFixture<InMemoryDatabaseFixture>, IAsyncLifetime
    {
        private readonly InMemoryDatabaseFixture _fx = fx;
        private ScenarioTestContext _ctx = null!;

        public async ValueTask InitializeAsync()
        {
            _ctx = await ScenarioTestContext.CreateAsync(_fx);
        }

        public async ValueTask DisposeAsync()
        {
            await _ctx.DisposeAsync();
        }

        [Fact]
        public void FilterViewModel_Object_Creation_Initialization()
        {
            var nameFilter = _ctx.MainVM.FilterPanelVM.Filters["Name"];
            Assert.NotEmpty(nameFilter.FilterOptions);

            Assert.True(_ctx.MainVM.FilterPanelVM.Filters.ContainsKey("SetName"), "Expected filter key 'SetName' not found.");
            var setNameFilter = _ctx.MainVM.FilterPanelVM.Filters["SetName"];
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
            var rarityFilter = _ctx.MainVM.FilterPanelVM.Filters["Rarity"];
            var expectedRarityOptions = new List<string> { "common", "uncommon", "rare", "mythic" };

            var actualRarityOptions = rarityFilter.FilterOptions
                .Select(opt => opt.OptionName)
                .OrderBy(x => x)
                .ToList();

            var sortedExpectedRarityOptions = expectedRarityOptions.OrderBy(x => x).ToList();
            Assert.Equal(sortedExpectedRarityOptions, actualRarityOptions);


            // Keywords:
            var keywordsFilter = _ctx.MainVM.FilterPanelVM.Filters["Keywords"];
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
            var subTypesFilter = _ctx.MainVM.FilterPanelVM.Filters["SubTypes"];
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
            var subTypesLabelFilter = _ctx.MainVM.FilterPanelVM.Filters["SubTypes"];
            Assert.Equal("Subtypes", subTypesLabelFilter.ReadableLabel);


            // SelectedCondition:
            var selectedConditionFilter = _ctx.MainVM.FilterPanelVM.Filters["SelectedCondition"];
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
            var selectedFinishFilter = _ctx.MainVM.FilterPanelVM.Filters["SelectedFinish"];
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
            var selectedFinishLabelFilter = _ctx.MainVM.FilterPanelVM.Filters["SelectedFinish"];
            Assert.Equal("Chosen finish", selectedFinishLabelFilter.ReadableLabel);

            // Language:
            var selectedLanguageFilter = _ctx.MainVM.FilterPanelVM.Filters["Language"];
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
            var colorFilter = _ctx.MainVM.FilterPanelVM.Filters["Colors"];
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
            var manaValueFilter = _ctx.MainVM.FilterPanelVM.Filters["ManaValue"];
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
}
