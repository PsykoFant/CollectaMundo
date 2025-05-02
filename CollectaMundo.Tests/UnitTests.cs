using CollectaMundo.Converters;
using CollectaMundo.Domain;
using CollectaMundo.DomainLogic.Models;
using CollectaMundo.Utilities;
using CollectaMundo.ViewModels;
using System.Globalization;
using System.Windows.Media.Imaging;
using static CollectaMundo.MainWindow;
using static CollectaMundo.Tests.FilterTestUtilities;


namespace CollectaMundo.Tests
{
    public class UnitTests
    {
        public class Filtering
        {
            private readonly static List<CardSet> cards = GetTestCards();
            public class FilterByNumericOptionsTests
            {

                [Fact]
                public void Test_NumericFilter_ManaValueGreaterThan3()
                {
                    // Arrange: build the domain filterLogic right here
                    var filterLogic = new FilterLogic(
                        criteriaKey: "ManaValue",
                        filterCategory: FilterType.Numeric,
                        selectedOptions: [],
                        selectedSingleOption: null,
                        selectedNumericValue: 3,
                        operatorSelection: OperatorType.GREATER_THAN,
                        defaultText: String.Empty
                    );

                    // Act: run it over your test cards
                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();

                    // Assert
                    Assert.All(result, card => Assert.True(card.ManaValue > 3));
                    Assert.Equal(9, result.Count);
                }

                [Fact]
                public void Test_NumericFilter_ManaValueEqual_To_Zero()
                {
                    var filterLogic = new FilterLogic(
                        criteriaKey: "ManaValue",
                        filterCategory: FilterType.Numeric,
                        selectedOptions: [],
                        selectedSingleOption: null,
                        selectedNumericValue: 0,
                        operatorSelection: OperatorType.EQUALS,
                        defaultText: String.Empty
                    );

                    // Apply the filter using the Matches method.
                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();

                    // Assert something about the resulting list.
                    Assert.True(result.All(card => card.ManaValue == 0));
                    Assert.Equal(3, result.Count);
                }
            }
            public class FilterBySingleOptionTests
            {
                [Fact]
                public void Test_SingleNameContains_Part_Of_Name()
                {
                    var filterLogic = new FilterLogic(
                        criteriaKey: "Name",
                        filterCategory: FilterType.Single,
                        selectedOptions: [],
                        selectedSingleOption: "fire",
                        selectedNumericValue: 0,
                        operatorSelection: OperatorType.OR,
                        defaultText: String.Empty
                    );

                    // Now filter the list
                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();

                    // Assert that only cards with "Lightning" in their name are returned.
                    Assert.Equal(2, result.Count);
                    Assert.Contains("Fire // Ice", result[0].Name);
                    Assert.Contains("Tarfire", result[1].Name);
                }

                [Fact]
                public void Test_SingleNameContains_Whole_Name()
                {
                    var filterLogic = new FilterLogic(
                        criteriaKey: "Name",
                        filterCategory: FilterType.Single,
                        selectedOptions: [],
                        selectedSingleOption: "Davros, Dalek Creator",
                        selectedNumericValue: 0,
                        operatorSelection: OperatorType.OR,
                        defaultText: String.Empty
                    );

                    // Now filter the list
                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();

                    // Assert that only cards with "Lightning" in their name are returned.
                    Assert.Single(result);
                    Assert.Contains("Davros, Dalek Creator", result[0].Name);
                }
            }
            public class FilterByMultiOptionsTests
            {

                [Fact]
                public void Test_MultiSelect_OR()
                {
                    var filterLogic = new FilterLogic(
                        criteriaKey: "Types",
                        filterCategory: FilterType.Multi,
                        selectedOptions: ["Sorcery", "Instant"],
                        selectedSingleOption: null,
                        selectedNumericValue: null,
                        operatorSelection: OperatorType.OR,
                        defaultText: String.Empty
                    );

                    // Filter cards using the Matches method.
                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();
                    Assert.Equal(6, result.Count);
                }

                [Fact]
                public void Test_MultiSelect_AND()
                {
                    var filterLogic = new FilterLogic(
                        criteriaKey: "Types",
                        filterCategory: FilterType.Multi,
                        selectedOptions: ["Artifact", "Creature"],
                        selectedSingleOption: null,
                        selectedNumericValue: null,
                        operatorSelection: OperatorType.AND,
                        defaultText: String.Empty
                    );

                    // Filter cards using the Matches method.
                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();
                    Assert.Equal(2, result.Count);
                }

                [Fact]
                public void Test_MultiSelect_NOT()
                {
                    var filterLogic = new FilterLogic(
                        criteriaKey: "Rarity",
                        filterCategory: FilterType.Multi,
                        selectedOptions: ["uncommon", "rare"],
                        selectedSingleOption: null,
                        selectedNumericValue: null,
                        operatorSelection: OperatorType.NOT,
                        defaultText: String.Empty
                    );

                    // Filter cards using the Matches method.
                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();
                    Assert.Equal(8, result.Count);
                }
            }
            public class FilterByColorTests
            {

                [Fact]
                public void Test_SingleColor_OR_Red()
                {
                    var filterLogic = new FilterLogic(
                        criteriaKey: "Colors",
                        filterCategory: FilterType.Multi,
                        selectedOptions: ["R"],
                        selectedSingleOption: null,
                        selectedNumericValue: null,
                        operatorSelection: OperatorType.OR,
                        defaultText: String.Empty
                    );

                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();

                    Assert.Equal(8, result.Count);
                }

                [Fact]
                public void Test_TwoColors_OR_G_R()
                {
                    var filterLogic = new FilterLogic(
                        criteriaKey: "Colors",
                        filterCategory: FilterType.Multi,
                        selectedOptions: ["R", "G"],
                        selectedSingleOption: null,
                        selectedNumericValue: null,
                        operatorSelection: OperatorType.OR,
                        defaultText: String.Empty
                    );

                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();

                    Assert.Equal(12, result.Count);
                }

                [Fact]
                public void Test_TwoColors_NOT_W_R()
                {
                    var filterLogic = new FilterLogic(
                        criteriaKey: "Colors",
                        filterCategory: FilterType.Multi,
                        selectedOptions: ["R", "W"],
                        selectedSingleOption: null,
                        selectedNumericValue: null,
                        operatorSelection: OperatorType.NOT,
                        defaultText: String.Empty
                    );

                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();

                    // Expected: Cards that do NOT have W or R.
                    Assert.Equal(9, result.Count);
                }

                [Fact]
                public void Test_TwoColors_AND_G_U()
                {
                    var filterLogic = new FilterLogic(
                        criteriaKey: "Colors",
                        filterCategory: FilterType.Multi,
                        selectedOptions: ["G", "U"],
                        selectedSingleOption: null,
                        selectedNumericValue: null,
                        operatorSelection: OperatorType.AND,
                        defaultText: String.Empty
                    );

                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();

                    // Expected: "Biomass Mutation" has Colors = "G, U".
                    Assert.Single(result);
                }
                [Fact]
                public void Test_SingleColor_OR_C()
                {
                    var filterLogic = new FilterLogic(
                        criteriaKey: "Colors",
                        filterCategory: FilterType.Multi,
                        selectedOptions: ["R", "C"],
                        selectedSingleOption: null,
                        selectedNumericValue: null,
                        operatorSelection: OperatorType.OR,
                        defaultText: String.Empty
                    );

                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();

                    Assert.Equal(9, result.Count);
                }

                [Fact]
                public void Test_NOT_R_NOT_C()
                {
                    var filterLogic = new FilterLogic(
                        criteriaKey: "Colors",
                        filterCategory: FilterType.Multi,
                        selectedOptions: ["R", "C"],
                        selectedSingleOption: null,
                        selectedNumericValue: null,
                        operatorSelection: OperatorType.NOT,
                        defaultText: String.Empty
                    );

                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();

                    Assert.Equal(9, result.Count);
                }

                [Fact]
                public void Test_SingleColor_AND_X()
                {
                    var filterLogic = new FilterLogic(
                        criteriaKey: "Colors",
                        filterCategory: FilterType.Multi,
                        selectedOptions: ["B", "X"],
                        selectedSingleOption: null,
                        selectedNumericValue: null,
                        operatorSelection: OperatorType.AND,
                        defaultText: String.Empty
                    );

                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();

                    Assert.Single(result);
                }

                [Fact]
                public void Test_TwoColors_AND_X()
                {
                    var filterLogic = new FilterLogic(
                        criteriaKey: "Colors",
                        filterCategory: FilterType.Multi,
                        selectedOptions: ["G", "U", "X"],
                        selectedSingleOption: null,
                        selectedNumericValue: null,
                        operatorSelection: OperatorType.AND,
                        defaultText: String.Empty
                    );

                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();

                    Assert.Single(result);
                }

                [Fact]
                public void Test_ThreeColors_AND_X()
                {
                    var filterLogic = new FilterLogic(
                        criteriaKey: "Colors",
                        filterCategory: FilterType.Multi,
                        selectedOptions: ["G", "U", "B", "X"],
                        selectedSingleOption: null,
                        selectedNumericValue: null,
                        operatorSelection: OperatorType.AND,
                        defaultText: String.Empty
                    );

                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();

                    Assert.Single(result);
                }

                [Fact]
                public void Test_Colorless_OR()
                {
                    var filterLogic = new FilterLogic(
                        criteriaKey: "Colors",
                        filterCategory: FilterType.Multi,
                        selectedOptions: ["Colorless"],
                        selectedSingleOption: null,
                        selectedNumericValue: null,
                        operatorSelection: OperatorType.OR,
                        defaultText: String.Empty
                    );

                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();

                    Assert.Equal(5, result.Count);
                }

                [Fact]
                public void Test_Colorless_X_NOT()
                {
                    var filterLogic = new FilterLogic(
                        criteriaKey: "Colors",
                        filterCategory: FilterType.Multi,
                        selectedOptions: ["Colorless", "X"],
                        selectedSingleOption: null,
                        selectedNumericValue: null,
                        operatorSelection: OperatorType.NOT,
                        defaultText: String.Empty
                    );

                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();

                    Assert.Equal(11, result.Count);
                }

                [Fact]
                public void Test_Colorless_AND_C()
                {
                    var filterLogic = new FilterLogic(
                        criteriaKey: "Colors",
                        filterCategory: FilterType.Multi,
                        selectedOptions: ["Colorless", "C"],
                        selectedSingleOption: null,
                        selectedNumericValue: null,
                        operatorSelection: OperatorType.AND,
                        defaultText: String.Empty
                    );

                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();

                    Assert.Single(result);
                }

                [Fact]
                public void Test_Colorless_AND_R()
                {
                    var filterLogic = new FilterLogic(
                        criteriaKey: "Colors",
                        filterCategory: FilterType.Multi,
                        selectedOptions: ["Colorless", "R"],
                        selectedSingleOption: null,
                        selectedNumericValue: null,
                        operatorSelection: OperatorType.AND,
                        defaultText: String.Empty
                    );

                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();

                    Assert.Empty(result);
                }

                [Fact]
                public void Test_Colorless_AND_C_AND_X()
                {
                    var filterLogic = new FilterLogic(
                        criteriaKey: "Colors",
                        filterCategory: FilterType.Multi,
                        selectedOptions: ["Colorless", "C", "X"],
                        selectedSingleOption: null,
                        selectedNumericValue: null,
                        operatorSelection: OperatorType.AND,
                        defaultText: String.Empty
                    );

                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();

                    Assert.Single(result);
                }
            }
        }
        public class Converters
        {
            // CountToSummaryConverter
            [Fact]
            public void Converter_Reflects_ViewModel_Counts()
            {
                // Arrange – populate the view‑model exactly as you already do in other tests
                var vm = new CardViewModel();
                vm.Cards.AddRange(FilterTestUtilities.GetTestCards());

                // pretend the user applied a filter that left 7 cards
                vm.FilteredCards = [.. vm.Cards.Take(7)];

                var converter = new CountToSummaryConverter();

                // Act
                var result = converter.Convert(
                    [vm.FilteredCards.Count, vm.Cards.Count],
                    typeof(string), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.Equal($"Showing 7 cards out of {vm.Cards.Count} cards.", result);
            }

            // StringToImageSourceConverter
            [WpfFact]
            public void Convert_NullOrEmpty_ReturnsNull()
            {
                // arrange
                var converter = new StringToImageSourceConverter();

                // act  (runs on an STA thread)
                var img1 = converter.Convert(null, typeof(BitmapImage), null, CultureInfo.InvariantCulture);
                var img2 = converter.Convert(string.Empty, typeof(BitmapImage), null, CultureInfo.InvariantCulture);

                // assert
                Assert.Null(img1);
                Assert.Null(img2);
            }

            [WpfFact]
            public void Convert_InvalidUri_ReturnsNull()
            {
                var converter = new StringToImageSourceConverter();
                const string bogus = "this-is-not-a-valid-uri";

                var result = converter.Convert(bogus, typeof(BitmapImage), null, CultureInfo.InvariantCulture);

                Assert.Null(result);
            }

            [WpfFact]
            public void Convert_ValidAbsoluteUri_ReturnsBitmapImageWithSameUri()
            {
                var converter = new StringToImageSourceConverter();
                const string url = "https://via.placeholder.com/50";

                var obj = converter.Convert(url, typeof(BitmapImage), null, CultureInfo.InvariantCulture);

                var bmp = Assert.IsType<BitmapImage>(obj);
                Assert.Equal(url, bmp.UriSource!.AbsoluteUri);
            }
        }
    }
}
