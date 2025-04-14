using CollectaMundo.ViewModels;
using System.Diagnostics;

namespace CollectaMundo.Tests
{
    public class FilterDefaultsIntegrationTests : IClassFixture<InMemoryDatabaseFixture>
    {
        private readonly InMemoryDatabaseFixture _fixture;

        public FilterDefaultsIntegrationTests(InMemoryDatabaseFixture fixture)
        {
            _fixture = fixture;
            // Ensure that the static DBAccess.connection points to our in-memory connection.
            DBAccess.connection = _fixture.Connection;
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
            // For example, if your in-memory seed data for "Name" includes a test card,
            // we expect that the "Name" filter options include that card name.
            Assert.True(filterVM.Filters.ContainsKey("Name"), "Expected filter key 'Name' not found.");
            var nameFilter = filterVM.Filters["Name"];

            foreach (var option in nameFilter.FilterOptions)
            {
                Debug.WriteLine($"Filter option: {option.OptionName}");
            }

            Assert.NotEmpty(nameFilter.FilterOptions);

            // Hardcoded lists of all expected names for the test.
            var expectedNames = new List<string>
            {
                "Karox Bladewing",
                "Zombie",
                "Island // Island",
                "All Will Be One // All Will Be One",
                "Silent Clearing // Silent Clearing",
                "Devil",
                "Cat",
                "Jan Jansen, Chaos Crafter // Jan Jansen, Chaos Crafter",
                "Ranger-Captain of Eos // Ranger-Captain of Eos",
                "Rampant Frogantua // Rampant Frogantua",
                "Sythis, Harvest's Hand // Sythis, Harvest's Hand",
                "Bloodvial Purveyor // Bloodvial Purveyor",
                "Shadrix Silverquill // Shadrix Silverquill",
                "Otter",
                "Warriors",
                "Blossoming Calm // Blossoming Calm",
                "Unblinking Observer // Unblinking Observer",
                "Dog",
                "Season of Weaving // Season of Weaving",
                "Goblin",
                "Deftblade Elite",
                "Gixian Puppeteer",
                "Bubbling Cauldron",
                "Angel of Glory's Rise",
                "The Thirteenth Doctor",
                "Spidersilk Armor",
                "Carrion Rats",
                "Sheoldred // The True Scriptures",
                "Boundary Lands Ranger",
                "Ancient Greenwarden",
                "Plains",
                "Forest",
                "Renounce",
                "Prismatic Vista",
                "Deny the Divine",
                "Staying Power",
                "Realmwalker",
                "Dead Weight",
                "Flameshot",
                "Nissa, Steward of Elements"
            };

            // Assert that the filter options contain all expected names.
            Assert.True(expectedNames.All(expected =>
                nameFilter.FilterOptions.Any(opt => opt.OptionName.Contains(expected))),
                "Not all expected filter options were found.");


            // Similarly, assert that the "SetName" filter was populated based on your seed data.
            Assert.True(filterVM.Filters.ContainsKey("SetName"), "Expected filter key 'SetName' not found.");
            var setNameFilter = filterVM.Filters["SetName"];
            Assert.NotEmpty(setNameFilter.FilterOptions);
            // Adjust expected value per your CSV seed.
            //Assert.Contains(setNameFilter.FilterOptions, opt => opt.Value.Contains("Judge Gift Cards"));

            // Optionally, verify the filter summary was updated.
            // For instance, if "Name" is a single–criteria filter that does not get default text (unless "Text"),
            // then your summary might be empty initially.
            // Adjust assertions based on your default logic.
            Assert.NotNull(filterVM.FilterSummary);
        }
    }
}
