using CollectaMundo.Converters;
using CollectaMundo.Models;
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
                    var numericFilter = CreateNumericFilter();
                    numericFilter.SelectedNumericValue = 3;
                    numericFilter.OperatorSelection = OperatorType.GREATER_THAN;

                    // Apply the filter using the Matches method.
                    var result = cards.Where(card => numericFilter.Matches(card)).ToList();

                    // Assert something about the resulting list.
                    Assert.True(result.All(card => card.ManaValue > 3));
                    Assert.Equal(9, result.Count);
                }

                [Fact]
                public void Test_NumericFilter_ManaValueEqual_To_Zero()
                {
                    var numericFilter = CreateNumericFilter();
                    numericFilter.SelectedNumericValue = 0;
                    numericFilter.OperatorSelection = OperatorType.EQUALS;

                    // Apply the filter using the Matches method.
                    var result = cards.Where(card => numericFilter.Matches(card)).ToList();

                    // Assert something about the resulting list.
                    Assert.True(result.All(card => card.ManaValue == 0));
                    Assert.Equal(3, result.Count);
                }
            }
            public class FilterByNameTests
            {
                [Fact]
                public void Test_SingleNameContains_Part_Of_Name()
                {
                    var nameFilter = CreateNameFilter();
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
                    var nameFilter = CreateNameFilter();
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
                    var multiFilter = CreateTypesFilter();
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
                    var multiFilter = CreateTypesFilter();
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
                    var multiFilter = CreateTypesFilter();
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
                    var multiFilter = CreateRarityFilter();
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
                    var multiFilter = CreateRarityFilter();
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
                    var colorFilter = CreateColorFilter();
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
                    var colorFilter = CreateColorFilter();
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
                    var colorFilter = CreateColorFilter();
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
                    var colorFilter = CreateColorFilter();
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
                    var colorFilter = CreateColorFilter();
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
                    var colorFilter = CreateColorFilter();
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
                    var colorFilter = CreateColorFilter();
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
                    var colorFilter = CreateColorFilter();
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
                    var colorFilter = CreateColorFilter();
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
                    var colorFilter = CreateColorFilter();
                    colorFilter.SelectedOptions.Clear();
                    colorFilter.SelectedOptions.Add("Colorless");
                    colorFilter.OperatorSelection = OperatorType.OR;

                    var result = cards.Where(card => colorFilter.Matches(card)).ToList();

                    Assert.Equal(5, result.Count);
                }

                [Fact]
                public void Test_Colorless_X_NOT()
                {
                    var colorFilter = CreateColorFilter();
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
                    var colorFilter = CreateColorFilter();
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
                    var colorFilter = CreateColorFilter();
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
                    var colorFilter = CreateColorFilter();
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
            [Fact]
            public void Convert_NullOrEmpty_ReturnsNull()
            {
                // arrange
                var converter = new StringToImageSourceConverter();

                // act  (runs on an STA thread)
                var img1 = RunOnSta(() => converter.Convert(null, typeof(BitmapImage), null, CultureInfo.InvariantCulture));
                var img2 = RunOnSta(() => converter.Convert(string.Empty, typeof(BitmapImage), null, CultureInfo.InvariantCulture));

                // assert
                Assert.Null(img1);
                Assert.Null(img2);
            }

            [Fact]
            public void Convert_InvalidUri_ReturnsNull()
            {
                var converter = new StringToImageSourceConverter();
                const string bogus = "this-is-not-a-valid-uri";

                var result = RunOnSta(() => converter.Convert(bogus, typeof(BitmapImage), null, CultureInfo.InvariantCulture));

                Assert.Null(result);
            }

            [Fact]
            public void Convert_ValidAbsoluteUri_ReturnsBitmapImageWithSameUri()
            {
                var converter = new StringToImageSourceConverter();
                const string url = "https://via.placeholder.com/50";   // any small, valid image URL

                var obj = RunOnSta(() => converter.Convert(url, typeof(BitmapImage), null, CultureInfo.InvariantCulture));

                var bmp = Assert.IsType<BitmapImage>(obj);
                Assert.Equal(url, bmp.UriSource!.AbsoluteUri);
            }

            private static T RunOnSta<T>(Func<T> action)
            {
                T? result = default;
                Exception? captured = null;
                var sta = new Thread(() =>
                {
                    try { result = action(); }
                    catch (Exception ex) { captured = ex; }
                });

                sta.SetApartmentState(ApartmentState.STA);
                sta.Start();
                sta.Join();

                // If the action threw on the STA thread, re-throw it here
                if (captured != null) throw captured;
                return result!;
            }
        }
    }
}
