using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace CollectaMundo.Converters
{
    /// <summary>
    /// Sets the font for the Colorless option in FilterColorsListBox
    /// </summary>
    public class ColorlessFontConverter : IValueConverter
    {
        /// <summary>
        /// Applies a different font size if the ListBoxItem content is "Colorless".
        /// </summary>
        /// <param name="value">The value passed from the binding (ListBoxItem).</param>
        /// <param name="targetType">The target property type.</param>
        /// <param name="parameter">The font size to apply if the condition is met.</param>
        /// <param name="culture">The culture information.</param>
        /// <returns>Modified font size based on the item's content.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ListBoxItem listBoxItem && listBoxItem.Content is string content)
            {
                if (content.Equals("Colorless", StringComparison.OrdinalIgnoreCase))
                {
                    return 13.90;  // Fixed font size
                }
            }
            return 0.01; // Default size for all other items
        }


        /// <summary>
        /// ConvertBack not implemented, as it's a one-way binding.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}

