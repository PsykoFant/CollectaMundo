using CollectaMundo.DomainLogic.Filtering.Models;

namespace CollectaMundo.DomainLogic.Filtering
{
    public interface IFilterItemSearchLogic
    {
        List<FilterOption> ApplyTextFilter(IEnumerable<FilterOption> allOptions, string filterText);
        List<string> ExtractSelectedOptions(IEnumerable<FilterOption> options);
        IEnumerable<FilterOption> BuildOptionsFromNames(IEnumerable<string> optionNames);
        bool IsEquivalentOptionList(IEnumerable<string> existing, IEnumerable<string> incoming);
        List<string> NormalizeOptionNames(IEnumerable<string> names);

        IEnumerable<FilterOption> BuildOptions(IEnumerable<FilterOption> options);
        bool IsEquivalentOptionList(IEnumerable<FilterOption> existing, IEnumerable<FilterOption> incoming);
    }
}
