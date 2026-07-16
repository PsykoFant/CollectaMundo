namespace CollectaMundo.DomainLogic.Shared;

public static class CsvValues
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
