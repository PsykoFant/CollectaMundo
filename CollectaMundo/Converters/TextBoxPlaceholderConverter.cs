using System.Globalization;
using System.Windows.Data;

namespace CollectaMundo.Converters
{
    public class TextBoxPlaceholderConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            string? filterText = values[0] as string;
            string? defaultText = values[1] as string;

            // Show default text when FilterText is empty or null
            return string.IsNullOrWhiteSpace(filterText) ? defaultText ?? "" : filterText;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            string? text = value as string;

            // If text is default text, return empty FilterText
            return new object[] { text == (parameter as string) ? "" : text, text };
        }
    }

}
