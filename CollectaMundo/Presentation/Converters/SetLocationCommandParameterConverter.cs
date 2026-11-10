using CollectaMundo.ViewModels.ModifyCollection.ParameterModels;
using System.Collections;
using System.Globalization;
using System.Windows.Data;

namespace CollectaMundo.Presentation.Converters
{
    public sealed class SetLocationCommandParameterConverter : IMultiValueConverter
    {
        public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || values[0] is not IEnumerable selectedItems)
            {
                return null;
            }

            var locationId = values[1] as int?;

            return new SetLocationForSelectedCardsParameter(selectedItems, locationId);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
