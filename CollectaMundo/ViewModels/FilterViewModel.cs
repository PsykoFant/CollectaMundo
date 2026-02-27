using CollectaMundo.ApplicationServices.Filtering;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CollectaMundo.ViewModels
{
    public partial class FilterViewModel(IFilteringService filteringService) : ObservableObject
    {
        // Injected dependencies
        private readonly IFilteringService _filteringService = filteringService;

        // Exposed filters and summary
        public Dictionary<string, FilterItemViewModel> Filters { get; } = [];

        [ObservableProperty]
        private string? filterSummary;
        public void NotifyFiltersRebuilt()
        {
            OnPropertyChanged(nameof(Filters));
        }

        public event EventHandler? FilterChanged;

        [RelayCommand]
        private void ClearFilters()
        {
            _filteringService.ResetAllFilters(Filters.Values);
            NotifyFilterChanged();
        }

        public void NotifyFilterChanged()
        {
            FilterSummary = _filteringService.BuildSummary(Filters.Values);
            FilterChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

