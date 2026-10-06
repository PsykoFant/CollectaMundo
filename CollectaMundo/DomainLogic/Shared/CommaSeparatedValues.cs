using System.Text;

namespace CollectaMundo.DomainLogic.Shared
{
    public static class CommaSeparatedValues
    {
        public static string[] Split(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv))
            {
                return [];
            }

            return csv.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);
        }
        public static string NormalizeAndDeduplicate(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv))
            {
                return string.Empty;
            }

            // Fast path: a single value cannot contain duplicates.
            if (csv.IndexOf(',') < 0)
            {
                return csv.Trim();
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var builder = new StringBuilder();

            foreach (var value in csv.Split(','))
            {
                var trimmed = value.Trim();

                if (trimmed.Length == 0 || !seen.Add(trimmed))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(',');
                }

                builder.Append(trimmed);
            }

            return builder.ToString();
        }
        public static string[] SplitPreservingThousandsSeparators(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return [];
            }

            if (value.IndexOf(',') < 0)
            {
                return [value.Trim()];
            }

            var parts = new List<string>();
            var start = 0;

            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] != ',')
                {
                    continue;
                }

                if (IsThousandsSeparator(value, i))
                {
                    continue;
                }

                AddPart(start, i - start);
                start = i + 1;
            }

            AddPart(start, value.Length - start);

            return [.. parts];

            void AddPart(int startIndex, int length)
            {
                var part = value.Substring(startIndex, length).Trim();

                if (part.Length > 0)
                {
                    parts.Add(part);
                }
            }
        }
        private static bool IsThousandsSeparator(string value, int commaIndex)
        {
            var firstDigit = commaIndex + 1;

            if (firstDigit + 2 >= value.Length)
            {
                return false;
            }

            if (!char.IsDigit(value[firstDigit]) || !char.IsDigit(value[firstDigit + 1]) || !char.IsDigit(value[firstDigit + 2]))
            {
                return false;
            }

            var afterDigits = firstDigit + 3;

            return afterDigits == value.Length || !char.IsLetterOrDigit(value[afterDigits]) && value[afterDigits] != '_';
        }
        public static bool Contains(string? csv, string value)
        {
            return Split(csv).Any(x => x.Equals(value, StringComparison.OrdinalIgnoreCase));
        }
        public static bool ContainsAll(string? csv, IEnumerable<string> values)
        {
            var set = Split(csv).ToHashSet(StringComparer.OrdinalIgnoreCase);

            return values.All(set.Contains);
        }
        public static bool ContainsAny(string? csv, IEnumerable<string> values)
        {
            var set = Split(csv).ToHashSet(StringComparer.OrdinalIgnoreCase);

            return values.Any(set.Contains);
        }
    }
}
