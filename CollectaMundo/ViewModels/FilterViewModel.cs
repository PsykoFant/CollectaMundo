using CollectaMundo.ApplicationServices;
using CollectaMundo.Data;
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
                _service.ResetAllFilters(Filters.Values);
                NotifyFilterChanged();
            });
        }

        public async Task InitializeAsync(IFilterDefaultsRepository defaultsRepo, IUnitOfWork uow)
        {
            await uow.BeginAsync();
            try
            {
                var defaults = await defaultsRepo.GetFilterDefaultsAsync();
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
                await uow.CommitAsync();
            }
            catch
            {
                await uow.RollbackAsync();
                throw;
            }
            finally
            {
                await uow.DisposeAsync();
            }
        }

        public void NotifyFilterChanged()
        {
            FilterSummary = _service.BuildSummary(Filters.Values);
            FilterChanged?.Invoke(this, EventArgs.Empty);
        }
    }

}

