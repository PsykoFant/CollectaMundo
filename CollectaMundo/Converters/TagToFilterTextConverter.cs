using CollectaMundo.Models;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Data;

namespace CollectaMundo.Converters
{
    public class TagToFilterTextConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (values == null || values.Length < 2)
                    return "Error: Missing Values";  // Prevent crashes

                if (values[0] is not string criteriaKey)
                    return "Error: Invalid Tag";  // Prevent crashes

                if (values[1] is not FilterViewModel filterVM)
                    return "Error: Invalid ViewModel";  // Prevent crashes

                return filterVM.GetDefaultTextByTag(criteriaKey);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TagToFilterTextConverter ERROR: {ex.Message}");
                return "Error"; // Prevent crash
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return new object[] { Binding.DoNothing, Binding.DoNothing }; // Corrected return type
        }
    }
}
