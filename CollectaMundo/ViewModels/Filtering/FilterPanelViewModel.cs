using CollectaMundo.ApplicationServices.Filtering;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CollectaMundo.ViewModels.Filtering
{
    public partial class FilterPanelViewModel(IFilteringService filteringService) : ObservableObject
    {
        private readonly IFilteringService _filteringService = filteringService;

        public event EventHandler? FilterChanged;
        public event EventHandler? FiltersRebuilt;

        public Dictionary<string, FilterItemViewModel> Filters { get; } = [];

        [ObservableProperty]
        private string? filterSummary;

        [RelayCommand]
        private void ClearFilters()
        {
            _filteringService.ResetAllFilters(Filters.Values);
            NotifyFilterChanged();
        }
        public void NotifyFiltersRebuilt()
        {
            FiltersRebuilt?.Invoke(this, EventArgs.Empty);
        }
        public void NotifyFilterChanged()
        {
            FilterSummary = _filteringService.BuildSummary(Filters.Values);
            FilterChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

