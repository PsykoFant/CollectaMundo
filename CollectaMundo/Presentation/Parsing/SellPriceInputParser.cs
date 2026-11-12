using System.Globalization;

namespace CollectaMundo.Presentation.Parsing;

public static class SellPriceInputParser
{
    public static bool TryParse(string? text, out decimal? value)
    {
        value = null;

        var input = text?.Trim();

        if (string.IsNullOrEmpty(input))
        {
            return true;
        }

        if (input.Any(character => !IsAsciiDigit(character) && character != '.' && character != ','))
        {
            return false;
        }

        var hasPeriod = input.Contains('.');
        var hasComma = input.Contains(',');

        if (hasPeriod && hasComma)
        {
            var periodIndex = input.LastIndexOf('.');
            var commaIndex = input.LastIndexOf(',');

            var decimalSeparator = periodIndex > commaIndex ? '.' : ',';
            var groupSeparator = decimalSeparator == '.' ? ',' : '.';
            var decimalIndex = input.LastIndexOf(decimalSeparator);

            var integerPart = input[..decimalIndex];
            var fractionalPart = input[(decimalIndex + 1)..];

            if (!IsValidGroupedInteger(integerPart, groupSeparator, allowUngrouped: true) ||
                !ContainsOnlyDigits(fractionalPart))
            {
                return false;
            }

            var normalizedInteger = integerPart.Replace(groupSeparator.ToString(), string.Empty);

            return TryCreatePrice(normalizedInteger, fractionalPart, out value);
        }

        if (hasPeriod || hasComma)
        {
            var separator = hasPeriod ? '.' : ',';
            var separatorCount = input.Count(character => character == separator);

            if (separatorCount > 1)
            {
                if (!IsValidGroupedInteger(input, separator, allowUngrouped: false))
                {
                    return false;
                }

                var normalizedInteger = input.Replace(separator.ToString(), string.Empty);

                return TryCreatePrice(normalizedInteger, string.Empty, out value);
            }

            var separatorIndex = input.IndexOf(separator);
            var integerPart = input[..separatorIndex];
            var fractionalPart = input[(separatorIndex + 1)..];

            if (fractionalPart.Length == 3 && integerPart.Length is >= 1 and <= 3 && ContainsOnlyDigits(integerPart) && ContainsOnlyDigits(fractionalPart))
            {
                return TryCreatePrice(string.Concat(integerPart, fractionalPart), string.Empty, out value);
            }

            if (integerPart.Length == 0 && fractionalPart.Length == 0)
            {
                return false;
            }

            if (integerPart.Length > 0 && !ContainsOnlyDigits(integerPart))
            {
                return false;
            }

            if (!ContainsOnlyDigits(fractionalPart))
            {
                return false;
            }

            return TryCreatePrice(integerPart.Length == 0 ? "0" : integerPart, fractionalPart, out value);
        }

        return ContainsOnlyDigits(input) && TryCreatePrice(input, string.Empty, out value);
    }
    private static bool TryCreatePrice(string integerPart, string fractionalPart, out decimal? value)
    {
        value = null;

        var digitsToKeep = Math.Min(2, fractionalPart.Length);
        var storedFraction = fractionalPart[..digitsToKeep].PadRight(2, '0');
        var normalized = $"{integerPart}.{storedFraction}";

        if (!decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        value = parsed;
        return true;
    }
    private static bool IsValidGroupedInteger(string text, char groupSeparator, bool allowUngrouped)
    {
        var groups = text.Split(groupSeparator);

        if (groups.Length == 1)
        {
            return allowUngrouped && ContainsOnlyDigits(groups[0]);
        }

        if (groups[0].Length is < 1 or > 3 || !ContainsOnlyDigits(groups[0]))
        {
            return false;
        }

        return groups
            .Skip(1)
            .All(group => group.Length == 3 && ContainsOnlyDigits(group));
    }
    private static bool ContainsOnlyDigits(string text)
    {
        return text.Length > 0 && text.All(IsAsciiDigit);
    }
    private static bool IsAsciiDigit(char character)
    {
        return character is >= '0' and <= '9';
    }
}
