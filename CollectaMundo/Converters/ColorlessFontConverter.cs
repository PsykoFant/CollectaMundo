using CollectaMundo.DomainLogic.Models;
using System.Globalization;
using System.Windows.Data;

namespace CollectaMundo.Converters
{
    // Sets the font for the Colorless option in FilterColorsListBox
    public class ColorlessFontConverter : IValueConverter
    {
        // Applies a different font size if the ListBoxItem content is "Colorless".
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is FilterOption filterOption)
            {
                if (filterOption.OptionName.Equals("Colorless", StringComparison.OrdinalIgnoreCase))
                {
                    return 13.90;  // Fixed font size for "Colorless"
                }
            }
            return 0.01; // Default size for all other items
        }

        // ConvertBack not implemented, as it's a one-way binding.        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}

