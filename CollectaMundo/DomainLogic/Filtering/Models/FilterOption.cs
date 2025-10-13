using CommunityToolkit.Mvvm.ComponentModel;

namespace CollectaMundo.DomainLogic.Filtering.Models
{
    public partial class FilterOption(string optionName, bool isSelected = false) : ObservableObject
    {
        public string OptionName { get; } = optionName;

        [ObservableProperty]
        private bool isSelected = isSelected;
    }
}
