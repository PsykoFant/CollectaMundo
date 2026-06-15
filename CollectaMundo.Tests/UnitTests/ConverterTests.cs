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
    }
}
