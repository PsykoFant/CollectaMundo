using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.Presentation.Converters;
using CollectaMundo.Tests.TestUtils;
using CollectaMundo.ViewModels.CardLists;
using System.Globalization;

namespace CollectaMundo.Tests.UnitTests
{
    public class ConverterTests
    {
        // CountToSummaryConverter
        [Fact]
        public void Converter_Reflects_ViewModel_Counts()
        {
            // Arrange – populate the view‑model exactly as you already do in other tests
            var vm = new CardListViewModel<PrintingCard>();
            vm.Cards.AddRange(TestCardFactory.GetTestPrintings());

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

        // CurrencyConversionLogic
        [Fact]
        public void Convert_UsdToEur_UsesRateAndRoundsToTwoDecimals()
        {
            var converted = CurrencyConversionLogic.Convert(100m, "USD", "EUR", 0.8949m);

            Assert.Equal(89.49m, converted);
        }

        [Fact]
        public void Convert_EurToUsd_UsesInverseRateAndRoundsToTwoDecimals()
        {
            var converted = CurrencyConversionLogic.Convert(100m, "EUR", "USD", 0.8909m);
            Assert.Equal(112.25m, converted);
        }

        [Fact]
        public void Convert_WhenCurrenciesMatch_ReturnsAmountUnchanged()
        {
            var converted = CurrencyConversionLogic.Convert(
                12.34m,
                "USD",
                "USD",
                0m);

            Assert.Equal(12.34m, converted);
        }

        [Fact]
        public void Convert_CrossCurrencyWithZeroRate_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CurrencyConversionLogic.Convert(1m, "USD", "EUR", 0m));
        }

        [Fact]
        public void Convert_UnsupportedSourceCurrency_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => CurrencyConversionLogic.Convert(1m, "GBP", "EUR", 0.89m));
        }
    }
}
