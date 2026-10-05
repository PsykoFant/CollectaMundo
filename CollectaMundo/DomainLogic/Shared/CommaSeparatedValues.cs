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
