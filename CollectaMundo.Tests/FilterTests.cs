using CollectaMundo.Managers;
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
            var filterVM = new FilterViewModel();

            // Set up filters: Name contains "Command" and ManaValue > 1.
            var nameFilter = filterVM.Filters["Name"];
            nameFilter.SelectedSingleOption = "Command";

            var numericFilter = filterVM.Filters["ManaValue"];
            numericFilter.SelectedNumericValue = 1;
            numericFilter.OperatorSelection = OperatorType.GREATER_THAN;

            // Act            
            var filteredCards = FilterManager.ApplyFilter(cardVM.Cards, filterVM.Filters.Values);

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
            var filterVM = new FilterViewModel();

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
            cardVM.FilteredCards = FilterManager.ApplyFilter(cardVM.Cards, filterVM.Filters.Values);

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

}
