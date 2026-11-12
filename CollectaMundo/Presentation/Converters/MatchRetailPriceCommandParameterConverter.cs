using CollectaMundo.ViewModels.ModifyCollection.ParameterModels;
using System.Collections;
using System.Globalization;
using System.Windows.Data;

namespace CollectaMundo.Presentation.Converters
{
    public sealed class MatchRetailPriceCommandParameterConverter : IMultiValueConverter
    {
        public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || values[0] is not IEnumerable selectedItems || values[1] is not string currency ||
                string.IsNullOrWhiteSpace(currency))
            {
                return null;
            }

            return new MatchRetailPriceForSelectedCardsParameter([.. selectedItems.Cast<object>()], currency);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
