using CollectaMundo.DomainLogic.Shared;
using System.Globalization;
using System.Windows.Data;

namespace CollectaMundo.Presentation.Converters
{
    public sealed class SellPriceDisplayConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 4 || values[0] is not decimal price || values[1] is not string sourceCurrency || string.IsNullOrWhiteSpace(sourceCurrency))
            {
                return string.Empty;
            }

            var targetCurrency = values[2] as string;

            if (string.IsNullOrWhiteSpace(targetCurrency))
            {
                return string.Empty;
            }

            sourceCurrency = sourceCurrency.ToUpperInvariant();
            targetCurrency = targetCurrency.ToUpperInvariant();

            if (sourceCurrency is not ("USD" or "EUR") ||
                targetCurrency is not ("USD" or "EUR"))
            {
                return string.Empty;
            }

            if (sourceCurrency == targetCurrency)
            {
                return Format(price, culture);
            }

            if (values[3] is not decimal rate || rate <= 0m)
            {
                return string.Empty;
            }

            try
            {
                var convertedPrice = CurrencyConversionLogic.Convert(price, sourceCurrency, targetCurrency, rate);

                return Format(convertedPrice, culture);
            }
            catch (ArgumentException)
            {
                return string.Empty;
            }
        }
        private static string Format(decimal price, CultureInfo culture)
        {
            return price.ToString("N2", culture);
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
