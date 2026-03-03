using CommunityToolkit.Mvvm.ComponentModel;

namespace CollectaMundo.ViewModels.Pages.SharedElements
{
    public sealed partial class FilterHeaderModel(string headerText) : ObservableObject
    {
        public string HeaderText { get; } = headerText;

        [ObservableProperty]
        private FilterItemViewModel? filterItem;
    }
}
