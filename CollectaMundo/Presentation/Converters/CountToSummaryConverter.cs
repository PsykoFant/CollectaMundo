using System.Globalization;
using System.Windows.Data;

namespace CollectaMundo.Presentation.Converters
{
    public class CountToSummaryConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            // Ensure we have two values.
            if (values.Length < 2)
            {
                return string.Empty;
            }

            // Try converting both values to int.
            if (values[0] is int filtered && values[1] is int total)
            {
                return $"Showing {filtered} cards out of {total} cards.";
            }
            return string.Empty;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
