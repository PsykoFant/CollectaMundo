using CollectaMundo.ApplicationServices.Filtering;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;


namespace CollectaMundo.ViewModels
{
    public partial class FilterViewModel(IFilteringService service) : ObservableObject
    {
        // Injected dependencies
        private readonly IFilteringService _service = service;

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
            _service.ResetAllFilters(Filters.Values);
            NotifyFilterChanged();
        }

        public void NotifyFilterChanged()
        {
            FilterSummary = _service.BuildSummary(Filters.Values);
            FilterChanged?.Invoke(this, EventArgs.Empty);
        }
    }

}

