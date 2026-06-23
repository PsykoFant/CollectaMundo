using CollectaMundo.ApplicationServices.Filtering;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace CollectaMundo.ViewModels.Filtering
{
    public partial class FilterPanelViewModel(IFilteringService filteringService) : ObservableObject
    {
        private readonly IFilteringService _filteringService = filteringService;

        public event EventHandler? FilterChanged;
        public event EventHandler? FiltersRebuilt;
        private bool _suppressFilterChanged;
        public Dictionary<string, FilterItemViewModel> Filters { get; } = [];
        public void BeginFilterChangeSuppression()
        {
            _suppressFilterChanged = true;
        }
        public void EndFilterChangeSuppression(bool notifyOnce = true)
        {
            _suppressFilterChanged = false;

            if (notifyOnce)
            {
                NotifyFilterChanged();
            }
        }

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
            if (_suppressFilterChanged)
            {
                return;
            }

            FiltersRebuilt?.Invoke(this, EventArgs.Empty);
        }
        public void NotifyFilterChanged([CallerMemberName] string caller = "")
        {
            Debug.WriteLine($"[Filter] NotifyFilterChanged from {caller}");

            if (_suppressFilterChanged)
            {
                return;
            }

            FilterSummary = _filteringService.BuildSummary(Filters.Values);

            FilterChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

