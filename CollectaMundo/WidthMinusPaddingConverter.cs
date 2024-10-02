using System.Globalization;
using System.Windows.Data;

namespace CollectaMundo
{
    public class WidthMinusPaddingConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double width && parameter is string paddingString && double.TryParse(paddingString, out double padding))
            {
                return Math.Max(0, width - padding); // Ensure width doesn't go negative
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("ConvertBack not supported.");
        }
    }

}
