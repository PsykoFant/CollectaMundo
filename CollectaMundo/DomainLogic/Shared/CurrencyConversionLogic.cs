namespace CollectaMundo.DomainLogic.Shared
{
    public static class CurrencyConversionLogic
    {
        public static decimal Convert(decimal amount, string sourceCurrency, string targetCurrency, decimal usdToEurRate)
        {
            if (string.IsNullOrWhiteSpace(sourceCurrency))
            {
                throw new ArgumentException("Source currency is required.", nameof(sourceCurrency));
            }

            if (string.IsNullOrWhiteSpace(targetCurrency))
            {
                throw new ArgumentException("Target currency is required.", nameof(targetCurrency));
            }

            var source = sourceCurrency.ToUpperInvariant();
            var target = targetCurrency.ToUpperInvariant();

            if (source is not ("USD" or "EUR"))
            {
                throw new ArgumentException($"Unsupported source currency '{sourceCurrency}'.", nameof(sourceCurrency));
            }

            if (target is not ("USD" or "EUR"))
            {
                throw new ArgumentException($"Unsupported target currency '{targetCurrency}'.", nameof(targetCurrency));
            }

            if (source == target)
            {
                return amount;
            }

            if (usdToEurRate <= 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(usdToEurRate), "The USD-to-EUR rate must be greater than zero.");
            }

            var convertedAmount = (source, target) switch
            {
                ("USD", "EUR") => amount * usdToEurRate,
                ("EUR", "USD") => amount / usdToEurRate,
                _ => throw new InvalidOperationException($"Cannot convert from '{source}' to '{target}'.")
            };

            return decimal.Round(convertedAmount, 2, MidpointRounding.AwayFromZero);
        }
    }
}
