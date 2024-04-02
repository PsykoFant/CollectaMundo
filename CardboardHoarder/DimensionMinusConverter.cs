using System.Globalization;
using System.Windows.Data;

namespace CardboardHoarder
{
    public class DimensionMinusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double dimension && parameter is string subtractionString && double.TryParse(subtractionString, out double subtraction))
            {
                return Math.Max(0, dimension - subtraction);  // Ensure the result is not negative
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}