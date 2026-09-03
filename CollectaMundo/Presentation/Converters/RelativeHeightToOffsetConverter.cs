using System.Globalization;
using System.Windows.Data;

namespace CollectaMundo.Presentation.Converters
{
    public sealed class RelativeHeightToOffsetConverter : IValueConverter
    {
        public object Convert(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            if (value is not double relativeHeight ||
                !double.TryParse(
                    parameter?.ToString(),
                    CultureInfo.InvariantCulture,
                    out var maxBarHeight))
            {
                return 0.0;
            }

            return -(relativeHeight * maxBarHeight + 2);
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
