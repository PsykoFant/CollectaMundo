using static CollectaMundo.MainWindow;

namespace CollectaMundo.Tests
{

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
