using CollectaMundo.DomainLogic.Filtering.Models;

namespace CollectaMundo.DomainLogic.Filtering
{
    public class FilterItemSearchLogic : IFilterItemSearchLogic
    {
        public List<FilterOption> ApplyTextFilter(IEnumerable<FilterOption> allOptions, string filterText)
        {
            if (string.IsNullOrWhiteSpace(filterText))
                return allOptions.ToList();

            return [.. allOptions.Where(option => option.OptionName.Contains(filterText, StringComparison.OrdinalIgnoreCase))];
        }
        public List<string> ExtractSelectedOptions(IEnumerable<FilterOption> options)
        {
            return [.. options
                .Where(o => o.IsSelected)
                .Select(o => o.OptionName)];
        }
        public IEnumerable<FilterOption> BuildOptionsFromNames(IEnumerable<string> optionNames)
        {
            return optionNames.Select(name => new FilterOption(name));
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
    }
}
