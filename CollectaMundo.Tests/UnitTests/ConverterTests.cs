using CollectaMundo.Presentation.Converters;
using CollectaMundo.Tests.TestUtils;
using CollectaMundo.ViewModels;
using System.Globalization;
using System.Windows.Media.Imaging;

namespace CollectaMundo.Tests.UnitTests
{
    public class ConverterTests
    {
        // CountToSummaryConverter
        [Fact]
        public void Converter_Reflects_ViewModel_Counts()
        {
            // Arrange – populate the view‑model exactly as you already do in other tests
            var vm = new CardViewModel();
            vm.Cards.AddRange(TestCardFactory.GetTestCards());

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
