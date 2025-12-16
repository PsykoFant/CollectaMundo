namespace CollectaMundo.ApplicationServices.Import
{
    public static class ImportValueNormalizer
    {
        public static List<string> SplitAndDistinct(
            IEnumerable<string> rawValues,
            char separator = ',')
        {
            return [.. rawValues
                .SelectMany(v => v
                    .Split(separator, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim()))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s)];
        }
    }

}
