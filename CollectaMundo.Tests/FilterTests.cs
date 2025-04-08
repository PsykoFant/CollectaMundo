using CollectaMundo.ViewModels;
using static CollectaMundo.MainWindow;

namespace CollectaMundo.Tests
{
    public class FilterIntegrationTests
    {
        [Fact]
        public void Test_CombinedNameAndNumericFilter()
        {
            // Arrange
            // Create a CardViewModel and populate it with test cards.
            var cardVM = new CardViewModel();
            var testCards = FilterTestUtilities.GetTestCards();
            // Simulate the loading process:
            cardVM.Cards.AddRange(testCards);
            // Initialize the filtered list to the full list initially.
            cardVM.FilteredCards = [.. cardVM.Cards];

            // Create a FilterViewModel based on the CardViewModel.
            var filterVM = new FilterViewModel(cardVM);

            // Set up filters: Name contains "Command" and ManaValue > 1.
            var nameFilter = filterVM.Filters["Name"];
            nameFilter.SelectedSingleOption = "Command";

            var numericFilter = filterVM.Filters["ManaValue"];
            numericFilter.SelectedNumericValue = 1;
            numericFilter.OperatorSelection = OperatorType.GREATER_THAN;

            // Act
            filterVM.ApplyFiltering();
            var filteredCards = cardVM.FilteredCards;

            string expectedSummary = "Name: \"Command\" AND ManaValue > 1";

            // Assert that the filter summary equals the expected string.
            Assert.Equal(expectedSummary, filterVM.FilterSummary);

            // Assert that every filtered card has a Name containing "Command" and a ManaValue > 1.
            Assert.All(filteredCards, card =>
            {
                Assert.Contains("Command", card.Name, StringComparison.OrdinalIgnoreCase);
                Assert.True(card.ManaValue > 1);
            });
        }


        [Fact]
        public void Test_Combined_Multi_Single_NumericFilter()
        {
            // Create a CardViewModel and populate it with test cards.
            var cardVM = new CardViewModel();
            var testCards = FilterTestUtilities.GetTestCards();
            // Simulate the loading process:
            cardVM.Cards.AddRange(testCards);
            // Initialize the filtered list to the full list initially.
            cardVM.FilteredCards = [.. cardVM.Cards];

            // Create a FilterViewModel based on the CardViewModel.
            var filterVM = new FilterViewModel(cardVM);

            // Filter on rulestext
            var textFilter = filterVM.Filters["Text"];
            textFilter.SelectedSingleOption = "damage";

            // Set a multi-select filter on "Colors":
            // For example, require that a card's Colors include either "R" or "G".
            var colorFilter = filterVM.Filters["Colors"];
            colorFilter.SelectedOptions.Clear();
            colorFilter.SelectedOptions.Add("R");
            colorFilter.SelectedOptions.Add("G");
            colorFilter.OperatorSelection = OperatorType.OR;

            var numericFilter = filterVM.Filters["ManaValue"];
            numericFilter.SelectedNumericValue = 3;
            numericFilter.OperatorSelection = OperatorType.GREATER_THAN_OR_EQUALS;

            // Act
            filterVM.ApplyFiltering();
            var filteredCards = cardVM.FilteredCards;

            // Assert
            Assert.All(filteredCards, card =>
            {
                Assert.Equal(5, filteredCards.Count);
                Assert.Contains("Olivia Voldaren", filteredCards[0].Name, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("Struggle // Survive", filteredCards[1].Name, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("Garruk Relentless // Garruk, the Veil-Cursed", filteredCards[2].Name, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("Fire // Ice", filteredCards[3].Name, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("Lukka, Bound to Ruin", filteredCards[4].Name, StringComparison.OrdinalIgnoreCase);
            });

            string expectedSummary = "Text: \"damage\" AND Colors: {R OR G} AND ManaValue >= 3";

            // Assert: Check that the FilterSummary property equals the expected string.
            Assert.Equal(expectedSummary, filterVM.FilterSummary);

        }
    }
    public class FilterByNumericOptionsTests
    {
        [Fact]
        public void Test_NumericFilter_ManaValueGreaterThan3()
        {
            var cards = FilterTestUtilities.GetTestCards();
            var numericFilter = FilterTestUtilities.CreateNumericFilter();
            // For example, set the numeric criterion to filter for ManaValue > 3.
            numericFilter.SelectedNumericValue = 3;
            numericFilter.OperatorSelection = OperatorType.GREATER_THAN;

            // Apply the filter using the Matches method.
            var result = cards.Where(card => numericFilter.Matches(card)).ToList();

            // Assert something about the resulting list.
            // (Change the expected count as appropriate for your test data.)
            Assert.True(result.All(card => card.ManaValue > 3));
            Assert.Equal(9, result.Count);
        }

        [Fact]
        public void Test_NumericFilter_ManaValueEqual_To_Zero()
        {
            var cards = FilterTestUtilities.GetTestCards();
            var numericFilter = FilterTestUtilities.CreateNumericFilter();
            // For example, set the numeric criterion to filter for ManaValue > 3.
            numericFilter.SelectedNumericValue = 0;
            numericFilter.OperatorSelection = OperatorType.EQUALS;

            // Apply the filter using the Matches method.
            var result = cards.Where(card => numericFilter.Matches(card)).ToList();

            // Assert something about the resulting list.
            // (Change the expected count as appropriate for your test data.)
            Assert.True(result.All(card => card.ManaValue == 0));
            Assert.Equal(3, result.Count);
        }
    }
    public class FilterByNameTests
    {
        [Fact]
        public void Test_SingleNameContains_Part_Of_Name()
        {
            var cards = FilterTestUtilities.GetTestCards(); // rich test set including a "Lightning Bolt"
            var nameFilter = FilterTestUtilities.CreateNameFilter(); // create a FilterItemViewModel for "Name"
                                                                     // For a single selection filter, set the SelectedSingleOption
            nameFilter.SelectedSingleOption = "fire";

            // Now filter the list
            var result = cards.Where(card => nameFilter.Matches(card)).ToList();

            // Assert that only cards with "Lightning" in their name are returned.
            Assert.Equal(2, result.Count);
            Assert.Contains("Fire // Ice", result[0].Name);
            Assert.Contains("Tarfire", result[1].Name);
        }

        [Fact]
        public void Test_SingleNameContains_Whole_Name()
        {
            var cards = FilterTestUtilities.GetTestCards(); // rich test set including a "Lightning Bolt"
            var nameFilter = FilterTestUtilities.CreateNameFilter(); // create a FilterItemViewModel for "Name"
                                                                     // For a single selection filter, set the SelectedSingleOption
            nameFilter.SelectedSingleOption = "Davros, Dalek Creator";

            // Now filter the list
            var result = cards.Where(card => nameFilter.Matches(card)).ToList();

            // Assert that only cards with "Lightning" in their name are returned.
            Assert.Single(result);
            Assert.Contains("Davros, Dalek Creator", result[0].Name);
        }
    }
    public class FilterByTypesTests
    {
        [Fact]
        public void Test_MultiSelect_Types_OR()
        {
            var cards = FilterTestUtilities.GetTestCards();
            var multiFilter = FilterTestUtilities.CreateTypesFilter();
            multiFilter.SelectedOptions.Clear();
            multiFilter.SelectedOptions.Add("Sorcery");
            multiFilter.SelectedOptions.Add("Instant");
            multiFilter.OperatorSelection = OperatorType.OR;

            // Filter cards using the Matches method.
            var result = cards.Where(card => multiFilter.Matches(card)).ToList();
            Assert.Equal(6, result.Count);
        }

        [Fact]
        public void Test_MultiSelect_Types_AND()
        {
            var cards = FilterTestUtilities.GetTestCards();
            var multiFilter = FilterTestUtilities.CreateTypesFilter();
            multiFilter.SelectedOptions.Clear();
            multiFilter.SelectedOptions.Add("Artifact");
            multiFilter.SelectedOptions.Add("Creature");
            multiFilter.OperatorSelection = OperatorType.AND;

            // Filter cards using the Matches method.
            var result = cards.Where(card => multiFilter.Matches(card)).ToList();
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void Test_MultiSelect_Types_NOT()
        {
            var cards = FilterTestUtilities.GetTestCards();
            var multiFilter = FilterTestUtilities.CreateTypesFilter();
            multiFilter.SelectedOptions.Clear();
            multiFilter.SelectedOptions.Add("Planeswalker");
            multiFilter.SelectedOptions.Add("Creature");
            multiFilter.OperatorSelection = OperatorType.NOT;

            // Filter cards using the Matches method.
            var result = cards.Where(card => multiFilter.Matches(card)).ToList();
            Assert.Equal(9, result.Count);
        }
    }
    public class FilterByRarityTests
    {
        [Fact]
        public void Test_MultiSelect_Rarity_OR()
        {
            var cards = FilterTestUtilities.GetTestCards();
            var multiFilter = FilterTestUtilities.CreateRarityFilter();
            multiFilter.SelectedOptions.Clear();
            multiFilter.SelectedOptions.Add("mythic");
            multiFilter.SelectedOptions.Add("rare");
            multiFilter.OperatorSelection = OperatorType.OR;

            // Filter cards using the Matches method.
            var result = cards.Where(card => multiFilter.Matches(card)).ToList();
            Assert.Equal(11, result.Count);
        }
        [Fact]
        public void Test_MultiSelect_Rarity_NOT()
        {
            var cards = FilterTestUtilities.GetTestCards();
            var multiFilter = FilterTestUtilities.CreateRarityFilter();
            multiFilter.SelectedOptions.Clear();
            multiFilter.SelectedOptions.Add("uncommon");
            multiFilter.SelectedOptions.Add("rare");
            multiFilter.OperatorSelection = OperatorType.NOT;

            // Filter cards using the Matches method.
            var result = cards.Where(card => multiFilter.Matches(card)).ToList();
            Assert.Equal(8, result.Count);
        }
    }
    public class FilterByColorTests
    {

        [Fact]
        public void Test_SingleColor_OR_Red()
        {
            var cards = FilterTestUtilities.GetTestCards();
            var colorFilter = FilterTestUtilities.CreateColorFilter();
            // For ANY filtering, set operator to OR and select "R".
            colorFilter.SelectedOptions.Clear();
            colorFilter.SelectedOptions.Add("R");
            colorFilter.OperatorSelection = OperatorType.OR;

            // Filter cards using the Matches method.
            var result = cards.Where(card => colorFilter.Matches(card)).ToList();

            // Expect only "Lightning Bolt" (which has Colors = "R")
            Assert.Equal(8, result.Count);
        }

        [Fact]
        public void Test_TwoColors_OR_G_R()
        {
            var cards = FilterTestUtilities.GetTestCards();
            var colorFilter = FilterTestUtilities.CreateColorFilter();
            colorFilter.SelectedOptions.Clear();
            colorFilter.SelectedOptions.Add("G");
            colorFilter.SelectedOptions.Add("R");
            colorFilter.OperatorSelection = OperatorType.OR;

            var result = cards.Where(card => colorFilter.Matches(card)).ToList();

            Assert.Equal(12, result.Count);
        }

        [Fact]
        public void Test_TwoColors_NOT_W_R()
        {
            var cards = FilterTestUtilities.GetTestCards();
            var colorFilter = FilterTestUtilities.CreateColorFilter();
            colorFilter.SelectedOptions.Clear();
            colorFilter.SelectedOptions.Add("W");
            colorFilter.SelectedOptions.Add("R");
            colorFilter.OperatorSelection = OperatorType.NOT;

            var result = cards.Where(card => colorFilter.Matches(card)).ToList();

            // Expected: Cards that do NOT have W or R.
            Assert.Equal(9, result.Count);
        }

        [Fact]
        public void Test_TwoColors_AND_G_U()
        {
            var cards = FilterTestUtilities.GetTestCards();
            var colorFilter = FilterTestUtilities.CreateColorFilter();
            colorFilter.SelectedOptions.Clear();
            colorFilter.SelectedOptions.Add("G");
            colorFilter.SelectedOptions.Add("U");
            colorFilter.OperatorSelection = OperatorType.AND;

            var result = cards.Where(card => colorFilter.Matches(card)).ToList();

            // Expected: "Biomass Mutation" has Colors = "G, U".
            Assert.Single(result);
        }
        [Fact]
        public void Test_SingleColor_AND_C()
        {
            var cards = FilterTestUtilities.GetTestCards();
            var colorFilter = FilterTestUtilities.CreateColorFilter();
            colorFilter.SelectedOptions.Clear();
            colorFilter.SelectedOptions.Add("R");
            colorFilter.SelectedOptions.Add("C");
            colorFilter.OperatorSelection = OperatorType.OR;

            var result = cards.Where(card => colorFilter.Matches(card)).ToList();

            Assert.Equal(9, result.Count);
        }

        [Fact]
        public void Test_NOT_R_NOT_C()
        {
            var cards = FilterTestUtilities.GetTestCards();
            var colorFilter = FilterTestUtilities.CreateColorFilter();
            colorFilter.SelectedOptions.Clear();
            colorFilter.SelectedOptions.Add("R");
            colorFilter.SelectedOptions.Add("C");
            colorFilter.OperatorSelection = OperatorType.NOT;

            var result = cards.Where(card => colorFilter.Matches(card)).ToList();

            Assert.Equal(9, result.Count);
        }

        [Fact]
        public void Test_SingleColor_AND_X()
        {
            var cards = FilterTestUtilities.GetTestCards();
            var colorFilter = FilterTestUtilities.CreateColorFilter();
            colorFilter.SelectedOptions.Clear();
            colorFilter.SelectedOptions.Add("B");
            colorFilter.SelectedOptions.Add("X");
            colorFilter.OperatorSelection = OperatorType.AND;

            var result = cards.Where(card => colorFilter.Matches(card)).ToList();

            Assert.Single(result);
        }

        [Fact]
        public void Test_TwoColors_AND_X()
        {
            var cards = FilterTestUtilities.GetTestCards();
            var colorFilter = FilterTestUtilities.CreateColorFilter();
            colorFilter.SelectedOptions.Clear();
            colorFilter.SelectedOptions.Add("G");
            colorFilter.SelectedOptions.Add("U");
            colorFilter.SelectedOptions.Add("X");
            colorFilter.OperatorSelection = OperatorType.AND;

            var result = cards.Where(card => colorFilter.Matches(card)).ToList();

            Assert.Single(result);
        }

        [Fact]
        public void Test_ThreeColors_AND_X()
        {
            var cards = FilterTestUtilities.GetTestCards();
            var colorFilter = FilterTestUtilities.CreateColorFilter();
            colorFilter.SelectedOptions.Clear();
            colorFilter.SelectedOptions.Add("G");
            colorFilter.SelectedOptions.Add("U");
            colorFilter.SelectedOptions.Add("B");
            colorFilter.SelectedOptions.Add("X");
            colorFilter.OperatorSelection = OperatorType.AND;

            var result = cards.Where(card => colorFilter.Matches(card)).ToList();

            Assert.Single(result);
        }

        [Fact]
        public void Test_Colorless_OR()
        {
            var cards = FilterTestUtilities.GetTestCards();
            var colorFilter = FilterTestUtilities.CreateColorFilter();
            colorFilter.SelectedOptions.Clear();
            colorFilter.SelectedOptions.Add("Colorless");
            colorFilter.OperatorSelection = OperatorType.OR;

            var result = cards.Where(card => colorFilter.Matches(card)).ToList();

            Assert.Equal(5, result.Count);
        }

        [Fact]
        public void Test_Colorless_X_NOT()
        {
            var cards = FilterTestUtilities.GetTestCards();
            var colorFilter = FilterTestUtilities.CreateColorFilter();
            colorFilter.SelectedOptions.Clear();
            colorFilter.SelectedOptions.Add("Colorless");
            colorFilter.SelectedOptions.Add("X");
            colorFilter.OperatorSelection = OperatorType.NOT;

            var result = cards.Where(card => colorFilter.Matches(card)).ToList();

            Assert.Equal(11, result.Count);
        }

        [Fact]
        public void Test_Colorless_AND_C()
        {
            var cards = FilterTestUtilities.GetTestCards();
            var colorFilter = FilterTestUtilities.CreateColorFilter();
            colorFilter.SelectedOptions.Clear();
            colorFilter.SelectedOptions.Add("Colorless");
            colorFilter.SelectedOptions.Add("C");
            colorFilter.OperatorSelection = OperatorType.AND;

            var result = cards.Where(card => colorFilter.Matches(card)).ToList();

            Assert.Single(result);
        }

        [Fact]
        public void Test_Colorless_AND_R()
        {
            var cards = FilterTestUtilities.GetTestCards();
            var colorFilter = FilterTestUtilities.CreateColorFilter();
            colorFilter.SelectedOptions.Clear();
            colorFilter.SelectedOptions.Add("Colorless");
            colorFilter.SelectedOptions.Add("R");
            colorFilter.OperatorSelection = OperatorType.AND;

            var result = cards.Where(card => colorFilter.Matches(card)).ToList();

            Assert.Empty(result);
        }

        [Fact]
        public void Test_Colorless_AND_C_AND_X()
        {
            var cards = FilterTestUtilities.GetTestCards();
            var colorFilter = FilterTestUtilities.CreateColorFilter();
            colorFilter.SelectedOptions.Clear();
            colorFilter.SelectedOptions.Add("Colorless");
            colorFilter.SelectedOptions.Add("C");
            colorFilter.SelectedOptions.Add("X");
            colorFilter.OperatorSelection = OperatorType.AND;

            var result = cards.Where(card => colorFilter.Matches(card)).ToList();

            Assert.Single(result);
        }
    }
}
