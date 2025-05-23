using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CollectaMundo.Presentation.Converters
{
    public class StringToVisibilityConverter : IValueConverter
    {
        // If the bound string is null/empty/whitespace → Collapsed; otherwise Visible
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var str = value as string;
            return string.IsNullOrWhiteSpace(str)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
