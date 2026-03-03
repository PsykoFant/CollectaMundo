using CommunityToolkit.Mvvm.ComponentModel;

namespace CollectaMundo.ViewModels.Pages.SharedElements
{
    public partial class FilterHeaderModel(string headerText, FilterItemViewModel filterItem, int colIndex, double initialComboWidth) : ObservableObject
    {
        public string HeaderText { get; } = headerText;
        public FilterItemViewModel FilterItem { get; } = filterItem;
        public int ColIndex { get; } = colIndex;

        [ObservableProperty]
        private double comboWidth = initialComboWidth;
    }
}
