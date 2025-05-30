using CollectaMundo.ViewModels;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CollectaMundo.Presentation.Converters
{
    public class PageToBrushConverter : IValueConverter
    {
        // Brush to use when the current page equals the parameter.
        public Brush SelectedBrush { get; set; } = Brushes.LightBlue;

        // Brush to use when they don’t match.
        public Brush DefaultBrush { get; set; } = Brushes.LightGray;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // value is the MainWindowVM’s CurrentPage, parameter is the Page enum we passed in XAML
            if (value is Page currentPage && parameter is Page buttonPage)
            {
                return currentPage == buttonPage
                    ? SelectedBrush
                    : DefaultBrush;
            }
            return DefaultBrush;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
