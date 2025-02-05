using CollectaMundo.Models;
using FluentAssertions;
using System.Diagnostics;
using System.Windows.Controls;
using static CollectaMundo.MainWindow;

namespace CollectaMundo.Tests
{
    public class FilterManagerTests
    {
        [Fact]
        public void ApplyFilter_NoFilters_ShouldNotChangeDataGrid()
        {
            // Arrange
            var cards = GetSampleCards();
            var dataGrid = new DataGrid();
            dataGrid.ItemsSource = cards.ToList(); // Initially set the DataGrid source

            // Ensure no filters are applied
            MainWindow.CurrentInstance.filterSelections.Clear();

            // Act
            FilterManagerOld.ApplyFilter(cards, dataGrid);

            // Assert
            dataGrid.ItemsSource.Should().BeEquivalentTo(cards.ToList()); // No change expected
        }

        [Fact]
        public void ApplyFilter_ValidFilter_ShouldFilterDataGrid()
        {
            // Arrange
            var cards = GetSampleCards();
            var dataGrid = new DataGrid();
            dataGrid.ItemsSource = cards.ToList();

            // Setup filter selection for a field that exists
            MainWindow.CurrentInstance.filterSelections.Clear();
            MainWindow.CurrentInstance.filterSelections.Add(new FilterSelections
            {
                CriteriaKey = "Rarity",
                MultipleCriteria = new HashSet<string> { "Rare" },
                Operator = OperatorType.OR
            });

            // Act
            FilterManagerOld.ApplyFilter(cards, dataGrid);

            // Assert
            var filteredCards = (List<CardSet>)dataGrid.ItemsSource!;
            filteredCards.Should().NotBeNullOrEmpty();
            filteredCards.Should().OnlyContain(card => card.Rarity == "Rare");
        }

        [Fact]
        public void ApplyFilter_NonExistentField_ShouldNotFilterDataGrid()
        {
            // Arrange
            var cards = GetSampleCards();
            var dataGrid = new DataGrid();
            dataGrid.ItemsSource = cards.ToList();

            // Setup filter selection for a non-existent field
            MainWindow.CurrentInstance.filterSelections.Clear();
            MainWindow.CurrentInstance.filterSelections.Add(new FilterSelections
            {
                CriteriaKey = "NonExistentField",
                MultipleCriteria = new HashSet<string> { "SomeValue" },
                Operator = OperatorType.OR
            });

            // Act
            FilterManagerOld.ApplyFilter(cards, dataGrid);

            // Assert
            dataGrid.ItemsSource.Should().BeEquivalentTo(cards.ToList()); // No change expected
        }

        [Fact]
        public void ApplyFilter_FilterSummaryShouldUpdate()
        {
            // Arrange
            var cards = GetSampleCards();
            var dataGrid = new DataGrid();
            dataGrid.ItemsSource = cards.ToList();

            // Setup filters
            MainWindow.CurrentInstance.filterSelections.Clear();
            MainWindow.CurrentInstance.filterSelections.Add(new FilterSelections
            {
                CriteriaKey = "Rarity",
                MultipleCriteria = new HashSet<string> { "Rare" },
                Operator = OperatorType.OR
            });

            // Act
            FilterManagerOld.ApplyFilter(cards, dataGrid);

            // Assert
            MainWindow.CurrentInstance.FilterSummaryTextBlock.Text.Should().Contain("Rarity");
        }

        [Fact]
        public void ApplyFilter_EmptyList_ShouldNotCrash()
        {
            // Arrange
            var emptyCards = new List<CardSet>(); // Empty list
            var dataGrid = new DataGrid();
            dataGrid.ItemsSource = emptyCards; // Set initial value

            // Act
            var exception = Record.Exception(() => FilterManagerOld.ApplyFilter(emptyCards, dataGrid));

            // Debugging output
            Debug.WriteLine($"Actual ItemsSource: {dataGrid.ItemsSource}");

            // Assert
            exception.Should().BeNull();
            dataGrid.ItemsSource.Should().NotBeNull("ItemsSource should always be assigned, even if empty.");
            dataGrid.ItemsSource.Should().BeAssignableTo<IEnumerable<CardSet>>("ItemsSource should be a list of CardSet.");
            ((IEnumerable<CardSet>)dataGrid.ItemsSource!).Should().BeEmpty(); // Check if truly empty
        }


        // Helper function to create sample cards
        private static List<CardSet> GetSampleCards()
        {
            return new List<CardSet>
            {
                new CardSet { Name = "Card1", Rarity = "Rare", Type = "Creature" },
                new CardSet { Name = "Card2", Rarity = "Common", Type = "Sorcery" },
                new CardSet { Name = "Card3", Rarity = "Rare", Type = "Enchantment" },
                new CardSet { Name = "Card4", Rarity = "Uncommon", Type = "Creature" }
            };
        }
    }
}
