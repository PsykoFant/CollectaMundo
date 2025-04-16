using CollectaMundo.Managers;
using CollectaMundo.ViewModels;
using static CollectaMundo.MainWindow;

namespace CollectaMundo.Tests
{
    public class IntegrationTests : IClassFixture<InMemoryDatabaseFixture>
    {
        private readonly InMemoryDatabaseFixture _fixture;

        public IntegrationTests(InMemoryDatabaseFixture fixture)
        {
            _fixture = fixture;
            // Ensure that the static DBAccess.connection points to our in-memory connection.
            DBAccess.connection = _fixture.Connection;
        }

        [Fact]
        public async Task CardListManager_PopulatesCardViewModels_FromViews()
        {
            // Arrange: Create fresh CardViewModel instances.
            var allCardsVM = new CardViewModel();
            var myCollectionVM = new CardViewModel();

            // Act: Populate the card lists.
            await CardListManager.CreateCardListObjectAsync(allCardsVM.Cards, CardListObject.AllCards);
            await CardListManager.CreateCardListObjectAsync(myCollectionVM.Cards, CardListObject.MyCollection);

            // Optional: Refresh any properties if your binding relies on notifications.
            // For integration tests that work on non-UI objects, you could directly assert on the collection count.

            // Assert: Check that the seed data was loaded.
            Assert.NotEmpty(allCardsVM.Cards);
            Assert.NotEmpty(myCollectionVM.Cards);

            //// For example, if your CSV seed for view_allCards contains 20 rows:
            Assert.Equal(59, allCardsVM.Cards.Count);
            Assert.Equal(22, myCollectionVM.Cards.Count);

            // 1. Define the expected list of names exactly in the order you want.
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

            // 2. Extract and sort the actual names from allCardsVM.
            var actualAllCardsNames = allCardsVM.Cards
                .Select(card => card.Name ?? string.Empty)
                .OrderBy(name => name)
                .ToList();

            // 3. Sort the expected list as well.
            var sortedAllcardsExpected = expectedAllCardsNames
                .OrderBy(name => name)
                .ToList();

            // 4. Assert that they match exactly (same elements, same count, same order).
            Assert.Equal(sortedAllcardsExpected, actualAllCardsNames);

            // Define the expected names.
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

            // Extract the actual names from myCollectionVM's Cards, ensuring non-null values.
            var actualMyCollectionNames = myCollectionVM.Cards
                .Select(card => card.Name!) // using null-forgiving operator if you're sure names are non-null
                .OrderBy(name => name)
                .ToList();

            // Sort expected names for comparison.
            var sortedMyCollectionExpected = expectedMyCollectionNames.OrderBy(name => name).ToList();

            // Assert that they are exactly the same.
            Assert.Equal(sortedMyCollectionExpected, actualMyCollectionNames);

        }

        [Fact]
        public async Task FilterViewModel_InitializeFilterDefaultsAsync_PopulatesFilters()
        {
            // Arrange: Create the FilterViewModel object.
            var filterVM = new FilterViewModel();

            // At this point, Filters has been pre-populated with stub FilterItemViewModel objects (if you preseed in the constructor)
            // or is empty; either way, the asynchronous initialization will update them.
            // Act: Initialize defaults using the in-memory database.
            await filterVM.InitializeFilterDefaultsAsync();

            // Assert: Verify that key filters are populated. 

            Assert.True(filterVM.Filters.ContainsKey("Name"), "Expected filter key 'Name' not found.");
            var nameFilter = filterVM.Filters["Name"];
            Assert.NotEmpty(nameFilter.FilterOptions);

            Assert.True(filterVM.Filters.ContainsKey("SetName"), "Expected filter key 'SetName' not found.");
            var setNameFilter = filterVM.Filters["SetName"];
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
            var rarityFilter = filterVM.Filters["Rarity"];
            var expectedRarityOptions = new List<string> { "common", "uncommon", "rare", "mythic" };

            var actualRarityOptions = rarityFilter.FilterOptions
                .Select(opt => opt.OptionName)
                .OrderBy(x => x)
                .ToList();

            var sortedExpectedRarityOptions = expectedRarityOptions.OrderBy(x => x).ToList();
            Assert.Equal(sortedExpectedRarityOptions, actualRarityOptions);


            // Keywords:
            var keywordsFilter = filterVM.Filters["Keywords"];
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

            var actualKeywordsOptions = keywordsFilter.FilterOptions
                .Select(opt => opt.OptionName)
                .OrderBy(x => x)
                .ToList();

            var sortedExpectedKeywordsOptions = expectedKeywordsOptions.OrderBy(x => x).ToList();
            Assert.Equal(sortedExpectedKeywordsOptions, actualKeywordsOptions);

            // Subtypes:
            var subTypesFilter = filterVM.Filters["SubTypes"];
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

        }
    }
}
