using CollectaMundo.Tests.TestUtils;
using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CollectaMundo.Tests.ScenarioTests
{
    public sealed class CardViewModelIntegrationTests(InMemoryDatabaseFixture fx) : IClassFixture<InMemoryDatabaseFixture>, IAsyncLifetime
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

            var actualAllCardsNames = _ctx.MainVM.AllCardsVM.Cards.Select(card => card.Name ?? string.Empty).OrderBy(name => name).ToList();
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
            var actualMyCollectionNames = _ctx.MainVM.MyCollectionVM.Cards.Select(card => card.Name ?? string.Empty).OrderBy(name => name).ToList();
            var sortedMyCollectionExpected = expectedMyCollectionNames.OrderBy(name => name).ToList();
            Assert.Equal(sortedMyCollectionExpected, actualMyCollectionNames);

            // Assert: total number of cards you physically own in MyCollection is 43
            var totalCardsOwned = _ctx.MainVM.MyCollectionVM.Cards.Sum(c => c.CardsOwned);
            Assert.Equal(43, totalCardsOwned);

            // Assert: total number of cards you physically own in CardsForTrade is 6
            var totalCardsForTrade = _ctx.MainVM.MyCollectionVM.Cards.Sum(c => c.CardsForTrade);
            Assert.Equal(6, totalCardsForTrade);

            // Assert: 15 entries are marked as Near Mint condition
            var nearMintCount = _ctx.MainVM.MyCollectionVM.Cards.Count(c => string.Equals(c.SelectedCondition, "Near Mint", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(15, nearMintCount);

            // Assert: 2 entries are marked as Good condition
            var goodCount = _ctx.MainVM.MyCollectionVM.Cards.Count(c => string.Equals(c.SelectedCondition, "Good", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(2, goodCount);

            // Assert: 19 entries are marked as English language
            var englishCount = _ctx.MainVM.MyCollectionVM.Cards.Count(c => string.Equals(c.Language, "English", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(19, englishCount);

            // Assert: 2 entries are marked as French language
            var frenchCount = _ctx.MainVM.MyCollectionVM.Cards.Count(c => string.Equals(c.Language, "French", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(2, frenchCount);

            // Assert: 18 entries are marked as nonfoil finish
            var nonfoilCount = _ctx.MainVM.MyCollectionVM.Cards.Count(c => string.Equals(c.SelectedFinish, "nonfoil", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(18, nonfoilCount);

            // Assert: 3 entries are marked as foil finish
            var foilCount = _ctx.MainVM.MyCollectionVM.Cards.Count(c => string.Equals(c.SelectedFinish, "foil", StringComparison.OrdinalIgnoreCase));
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

            foreach (var card in _ctx.MainVM.AllCardsVM.Cards)
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

            foreach (var card in _ctx.MainVM.MyCollectionVM.Cards)
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

            foreach (var card in _ctx.MainVM.AllCardsVM.Cards)
            {
                var setCode = card.SetCode ?? string.Empty;

                if (!string.IsNullOrEmpty(setCode) &&
                    validSetCodes.Contains(setCode))
                {
                    var image = card.KeyRuneImage;

                    if (image == null)
                    {
                        Debug.WriteLine($"Missing SetIconImage for card '{card.Name}' set '{setCode}'");
                    }

                    Assert.NotNull(image);
                    Assert.IsAssignableFrom<ImageSource>(image);

                    if (image is BitmapImage bmp)
                    {
                        Assert.True(bmp.IsFrozen, "Bitmap should be frozen for thread safety.");
                    }
                }
            }

            foreach (var card in _ctx.MainVM.MyCollectionVM.Cards)
            {
                var setCode = card.SetCode ?? string.Empty;

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
}
