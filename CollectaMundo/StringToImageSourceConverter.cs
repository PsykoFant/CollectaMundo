using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace CollectaMundo
{
    public class StringToImageSourceConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var imageUrl = value as string;
            if (string.IsNullOrEmpty(imageUrl))
                return null;

            try
            {
                return new BitmapImage(new Uri(imageUrl, UriKind.Absolute));
            }
            catch
            {
                return null;  // In case the URL is not valid
            }
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}