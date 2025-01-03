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
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ListBoxItem listBoxItem && listBoxItem.Content is string content)
            {
                var listBox = ItemsControl.ItemsControlFromItemContainer(listBoxItem) as ListBox;
                if (listBox != null && listBox.Items.Count > 0 &&
                    content == listBox.Items[listBox.Items.Count - 1].ToString())
                {
                    return double.TryParse(parameter?.ToString(), out double fontSize) ? fontSize : 13.90;
                }
            }

            return 0.01; // Default font size
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}

