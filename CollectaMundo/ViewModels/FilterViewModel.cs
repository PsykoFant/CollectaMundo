using CollectaMundo.ApplicationServices.Filtering;
using CollectaMundo.Utilities;
using System.ComponentModel;
using System.Windows.Input;


namespace CollectaMundo.ViewModels
{
    public class FilterViewModel : INotifyPropertyChanged
    {
        // Injected dependencies
        private readonly IFilteringService _service;

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
        public FilterViewModel(IFilteringService service)
        {

            _service = service;

            ClearFiltersCommand = new RelayCommand<object>(_ =>
            {
                _service.ResetAllFilters(Filters.Values);
                NotifyFilterChanged();
            });
        }
        public void NotifyFilterChanged()
        {
            FilterSummary = _service.BuildSummary(Filters.Values);
            FilterChanged?.Invoke(this, EventArgs.Empty);
        }
    }

}

