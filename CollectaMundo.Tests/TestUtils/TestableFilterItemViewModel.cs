using CollectaMundo.DomainLogic.Filtering;
using CollectaMundo.DomainLogic.Filtering.Models;
using CollectaMundo.ViewModels;

namespace CollectaMundo.Tests.TestUtils
{
    public class TestableFilterItemViewModel : FilterItemViewModel
    {
        public TestableFilterItemViewModel(
            string criteriaKey,
            IEnumerable<FilterOption> filterOptions,
            string defaultText,
            string readableLabel,
            FilterViewModel filterViewModel,
            IFilterItemSearchLogic filterItemSearchLogic,
            IEnumerable<int>? numericOptions = null)
            : base(criteriaKey, filterOptions, defaultText, readableLabel, filterViewModel, filterItemSearchLogic, numericOptions)
        {
        }

        public void SimulateTypingComplete()
        {
            // This method must exist in FilterItemViewModel as protected or protected internal
            ApplyTypingSelection();
        }
    }
}
