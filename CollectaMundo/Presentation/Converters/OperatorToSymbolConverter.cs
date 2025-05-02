using System.Globalization;
using System.Windows.Data;
using static CollectaMundo.MainWindow;

namespace CollectaMundo.Presentation.Converters
{
    public class OperatorToSymbolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is OperatorType op)
            {
                return op switch
                {
                    OperatorType.LESS_THAN => "Less than:",
                    OperatorType.LESS_THAN_OR_EQUALS => "Less than or equals:",
                    OperatorType.GREATER_THAN => "Greater than:",
                    OperatorType.GREATER_THAN_OR_EQUALS => "Greater than or equals:",
                    OperatorType.EQUALS => "Equals:",
                    OperatorType.NOT_EQUALS => "Does not equal:",
                    _ => op.ToString()
                };
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
