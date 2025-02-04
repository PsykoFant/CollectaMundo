using System.Globalization;
using System.Windows.Data;

namespace CollectaMundo.Converters
{
    public class HashSetContainsConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is HashSet<string> hashSet && parameter is string item)
            {
                return hashSet.Contains(item);
            }
            return false;
        }

        public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing; // We handle updates manually in FilterSelections
        }
    }
}

