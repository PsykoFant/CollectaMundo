using CollectaMundo.DomainLogic.Filtering.Models;

namespace CollectaMundo.DomainLogic.Filtering
{
    public class FilterItemSearchLogic : IFilterItemSearchLogic
    {
        public List<FilterOption> ApplyTextFilter(IEnumerable<FilterOption> allOptions, string filterText)
        {
            if (string.IsNullOrWhiteSpace(filterText))
            {
                return [.. allOptions];
            }

            return [.. allOptions.Where(option => option.DisplayName.Contains(filterText, StringComparison.OrdinalIgnoreCase))];
        }
        public List<string> ExtractSelectedOptions(IEnumerable<FilterOption> options)
        {
            return [.. options
                .Where(o => o.IsSelected)
                .Select(o => o.Value)];
        }
        public IEnumerable<FilterOption> BuildOptionsFromNames(IEnumerable<string> optionNames)
        {
            return optionNames.Select(name => new FilterOption(name, name));
        }
        public bool IsEquivalentOptionList(IEnumerable<string> existing, IEnumerable<string> incoming)
        {
            var cleanedIncoming = incoming
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var cleanedExisting = existing
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return cleanedExisting.SequenceEqual(cleanedIncoming, StringComparer.OrdinalIgnoreCase);
        }
        public List<string> NormalizeOptionNames(IEnumerable<string> names) =>
            [.. names
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)];

        public IEnumerable<FilterOption> BuildOptions(IEnumerable<FilterOption> options)
        {
            return options.Select(option => new FilterOption(option.Value, option.DisplayName));
        }
        public bool IsEquivalentOptionList(IEnumerable<FilterOption> existing, IEnumerable<FilterOption> incoming)
        {
            var existingList = existing
                .OrderBy(o => o.Value, StringComparer.OrdinalIgnoreCase)
                .ThenBy(o => o.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(o => (o.Value, o.DisplayName))
                .ToList();

            var incomingList = incoming
                .OrderBy(o => o.Value, StringComparer.OrdinalIgnoreCase)
                .ThenBy(o => o.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(o => (o.Value, o.DisplayName))
                .ToList();

            return existingList.SequenceEqual(incomingList);
        }
    }
}
