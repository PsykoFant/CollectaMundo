using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace CollectaMundo.Presentation.Converters
{
    public class StringToImageSourceConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var imageUrl = value as string;
            if (string.IsNullOrEmpty(imageUrl))
                return null;

            try
            {
                var uri = new Uri(imageUrl, UriKind.Absolute);
                return new BitmapImage(uri);
            }
            catch
            {
                return null;  // In case the URI is not valid
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

}