using CollectaMundo.ViewModels.Pages.Models;
using System.Collections;
using System.Globalization;
using System.Windows.Data;

namespace CollectaMundo.Presentation.Converters
{
    public sealed class AddCardsToDeckCommandParameterConverter : IMultiValueConverter
    {
        public object? Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Length < 2 || values[0] is not IEnumerable selectedItems || values[1] is not int deckLocationId)
            {
                return null;
            }

            return new AddCardsToDeckParameter(selectedItems.Cast<object>(), deckLocationId);
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
