using CommunityToolkit.Mvvm.ComponentModel;

namespace CollectaMundo.DomainLogic.Filtering.Models
{
    // Base class for all filters with common properties.
    public abstract class FilterBase : ObservableObject
    {
        public required string CriteriaKey { get; set; }
    }
    public partial class FilterDefaults : FilterBase
    {
        public List<FilterOption> FilterOptions { get; set; } = [];
        public List<int>? NumericCriteria { get; set; }

        [ObservableProperty]
        private string readableLabel = string.Empty;

        [ObservableProperty]
        private string defaultText = string.Empty;
    }
}

