using CollectaMundo.ViewModels.Shell;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CollectaMundo.Presentation.Converters
{
    // Converts your MainWindowVM’s CurrentPage + a ConverterParameter Page enum
    // into a Visibility (Visible when they match, Collapsed otherwise).
    public class PageToVisibilityConverter : IValueConverter
    {
        // What to return when CurrentPage == ConverterParameter.
        public Visibility VisibleVisibility { get; set; } = Visibility.Visible;

        // What to return when they don’t match.
        public Visibility HiddenVisibility { get; set; } = Visibility.Collapsed;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ShellPageEnum currentPage && parameter is ShellPageEnum buttonPage)
            {
                return currentPage == buttonPage
                    ? VisibleVisibility
                    : HiddenVisibility;
            }

            // fallback if either isn’t the right type
            return HiddenVisibility;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
