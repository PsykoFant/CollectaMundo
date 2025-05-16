using CollectaMundo.UICoordinators;
using CollectaMundo.Utilities;
using System.ComponentModel;
using System.Windows.Input;


namespace CollectaMundo.ViewModels
{
    public class FilterViewModel : INotifyPropertyChanged
    {
        // Injected dependencies
        private readonly IFilteringService _coord;

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
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // Constructor now takes interfaces
        public FilterViewModel(IFilteringService coord)
        {
            _coord = coord;
            // pre-populate empty so bindings don’t break…
            foreach (var key in FilterCriteriaMappings.CriteriaMappings.Keys)
                Filters[key] = new FilterItemViewModel(
                  key,
                  [],
                  defaultText: string.Empty,
                  readableLabel: string.Empty,
                  filterViewModel: this,
                  numericOptions: null
                );

            ClearFiltersCommand = new RelayCommand<object>(_ =>
            {
                _coord.ResetAllFilters(Filters.Values);
                NotifyFilterChanged();
            });
        }
        public async Task InitializeFilterDefaultsAsync()
        {
            var defs = await _coord.LoadDefaultsAsync();
            foreach (var d in defs)
            {
                Filters[d.CriteriaKey] = new FilterItemViewModel(
                    d.CriteriaKey,
                    d.FilterOptions,
                    d.DefaultText,
                    d.ReadableLabel,
                    filterViewModel: this,
                    numericOptions: d.NumericCriteria
                );
            }
            NotifyFilterChanged();
        }

        // Called by each FilterItemViewModel on change
        public void NotifyFilterChanged()
        {
            FilterSummary = _coord.BuildSummary(Filters.Values);
            FilterChanged?.Invoke(this, EventArgs.Empty);
        }
    }

}

