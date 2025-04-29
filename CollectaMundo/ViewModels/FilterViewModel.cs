using CollectaMundo.Data;
using CollectaMundo.UICoordinators;
using CollectaMundo.Utilities;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace CollectaMundo.ViewModels
{
    public class FilterViewModel : INotifyPropertyChanged
    {
        // Injected dependencies
        private readonly IFilterDefaultsRepository _defaultsRepo;
        private readonly IFilteringCoordinator _coord;

        // Exposed filters and summary
        public Dictionary<string, FilterItemViewModel> Filters { get; } = [];
        private string? _filterSummary;
        public string? FilterSummary
        {
            get => _filterSummary;
            set
            {
                if (_filterSummary != value)
                {
                    _filterSummary = value;
                    OnPropertyChanged(nameof(FilterSummary));
                }
            }
        }

        public ICommand ClearFiltersCommand { get; }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? FilterChanged;
        protected void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // Constructor now takes interfaces
        public FilterViewModel(IFilterDefaultsRepository defaultsRepo, IFilteringCoordinator filterService)
        {
            _defaultsRepo = defaultsRepo ?? throw new ArgumentNullException(nameof(defaultsRepo));
            _coord = filterService ?? throw new ArgumentNullException(nameof(filterService));

            // Prepopulate with “empty” items so UI can bind before defaults load
            foreach (var key in FilterCriteriaMappings.CriteriaMappings.Keys)
            {
                Filters[key] = new FilterItemViewModel(key, [], defaultText: string.Empty, readableLabel: string.Empty, this, numericOptions: null);
            }

            ClearFiltersCommand = new RelayCommand<object>(_ =>
            {
                _coord.ResetAllFilters(Filters.Values);
                NotifyFilterChanged();
            });
        }

        // Loads defaults from the repository instead of FilterManager
        public async Task InitializeFilterDefaultsAsync()
        {
            try
            {
                var defaults = await _defaultsRepo.GetFilterDefaultsAsync();
                foreach (var def in defaults)
                {
                    Filters[def.CriteriaKey] = new FilterItemViewModel(
                        def.CriteriaKey,
                        def.FilterOptions,
                        def.DefaultText,
                        def.ReadableLabel,
                        this,
                        def.NumericCriteria);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing filter defaults: {ex.Message}");
                MessageBox.Show($"Error initializing filters: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Called by each FilterItemViewModel on change
        public void NotifyFilterChanged()
        {
            FilterSummary = _coord.BuildSummary(Filters.Values);
            FilterChanged?.Invoke(this, EventArgs.Empty);
        }
    }

}

