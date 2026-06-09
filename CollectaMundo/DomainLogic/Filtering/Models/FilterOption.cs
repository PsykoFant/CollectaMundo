using CommunityToolkit.Mvvm.ComponentModel;

namespace CollectaMundo.DomainLogic.Filtering.Models
{
    public partial class FilterOption(string value, string displayName, bool isSelected = false) : ObservableObject
    {
        public string Value { get; } = value;
        public string DisplayName { get; } = displayName;

        // Temporary compatibility alias while refactoring
        public string OptionName => DisplayName;

        [ObservableProperty]
        private bool isSelected = isSelected;
    }
}
