using CollectaMundo.Managers;
using CollectaMundo.Utilities;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using static CollectaMundo.MainWindow;

namespace CollectaMundo.ViewModels
{
    public class FilterViewModel : INotifyPropertyChanged
    {
        public Dictionary<string, FilterItemViewModel> Filters { get; } = [];

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        // Event that signals the filters have changed.
        public event EventHandler? FilterChanged;

        // Update the filter summary
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
        public ICommand? ClearFiltersCommand { get; }

        // Constructor
        public FilterViewModel()
        {
            // Prepopulate Filters with empty/stub FilterItemViewModel objects for all keys.
            foreach (var key in FilterCriteriaMappings.CriteriaMappings.Keys)
            {
                Filters[key] = new FilterItemViewModel(key, [], string.Empty, string.Empty, this, null);
            }
            // Initialize the command using the ClearFilters method.
            ClearFiltersCommand = new RelayCommand<object>(_ => ClearFilters());
        }

        // Initialize the filter defaults from the database.
        public async Task InitializeFilterDefaultsAsync()
        {
            Stopwatch sw = Stopwatch.StartNew();

            try
            {
                var filterDefaults = await FilterManager.GetFilterDefaultsFromDBAsync();
                foreach (var filter in filterDefaults)
                {
                    Filters[filter.CriteriaKey] = new FilterItemViewModel(
                        filter.CriteriaKey,
                        filter.FilterOptions,
                        filter.DefaultText,
                        filter.ReadableLabel,
                        this,
                        filter.NumericCriteria
                    );
                }

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing filter defaults: {ex.Message}");
                MessageBox.Show($"Error initializing filter defaults: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            sw.Stop();
            Debug.WriteLine($"Filter defaults took: {sw.ElapsedMilliseconds} ms");
        }

        // Called by FilterItemViewModel instances when a filter value changes. This method raises the FilterChanged event.
        public void NotifyFilterChanged()
        {
            UpdateFilterSummary();
            FilterChanged?.Invoke(this, EventArgs.Empty);
        }

        // Update the filter summary based on the current filter selections.
        private void UpdateFilterSummary()
        {
            try
            {
                var summary = new StringBuilder();

                foreach (var filter in Filters.Values)
                {
                    switch (filter.FilterCategory)
                    {
                        case FilterType.Single:
                            if (!string.IsNullOrWhiteSpace(filter.SelectedSingleOption) &&
                                filter.SelectedSingleOption != filter.DefaultText)
                            {
                                summary.Append($"{filter.CriteriaKey}: \"{filter.SelectedSingleOption}\" AND ");
                            }
                            break;

                        case FilterType.Multi:
                            if (filter.SelectedOptions != null && filter.SelectedOptions.Any())
                            {
                                // Determine the operator symbol.
                                string operatorSymbol = filter.OperatorSelection switch
                                {
                                    OperatorType.OR => "OR",
                                    OperatorType.AND => "AND",
                                    OperatorType.NOT => "AND",  // We join with "AND" but prefix each option with "NOT"
                                    _ => ""
                                };

                                // Build the filter segment. If the operator is NOT, prefix each option with "NOT ".
                                string filterSegment = filter.OperatorSelection == OperatorType.NOT
                                    ? string.Join($" {operatorSymbol} ", filter.SelectedOptions.Select(opt => $"NOT {opt}"))
                                    : string.Join($" {operatorSymbol} ", filter.SelectedOptions);

                                summary.Append($"{filter.CriteriaKey}: {{{filterSegment}}} AND ");
                            }
                            break;

                        case FilterType.Numeric:
                            if (filter.SelectedNumericValue != null)
                            {
                                summary.Append($"{filter.CriteriaKey} {GetOperatorSymbol(filter.OperatorSelection)} {filter.SelectedNumericValue} AND ");
                            }
                            break;
                    }
                }

                // Remove the trailing " AND " if present.
                if (summary.Length >= 5)
                {
                    summary.Remove(summary.Length - 5, 5);
                }

                FilterSummary = summary.ToString();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating filter summary: {ex.Message}");
                MessageBox.Show($"Error updating filter summary: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private static string GetOperatorSymbol(OperatorType op)
        {
            return op switch
            {
                OperatorType.LESS_THAN => "<",
                OperatorType.LESS_THAN_OR_EQUALS => "<=",
                OperatorType.GREATER_THAN => ">",
                OperatorType.GREATER_THAN_OR_EQUALS => ">=",
                OperatorType.EQUALS => "==",
                OperatorType.NOT_EQUALS => "!=",
                _ => ""
            };
        }

        // Clears all filter selections, resetting each filter to its default state.
        private void ClearFilters()
        {
            foreach (var filter in Filters.Values)
            {
                switch (filter.FilterCategory)
                {
                    case FilterType.Single:
                        filter.FreetextSearch = filter.DefaultText;
                        filter.FilterText = filter.DefaultText;
                        filter.SelectedSingleOption = null;
                        filter.TextForeground = Brushes.Gray;
                        break;


                    case FilterType.Multi:
                        // Uncheck each option so that the UI updates and SelectedOptions is recalculated.
                        foreach (var option in filter.FilterOptions)
                        {
                            option.IsSelected = false;
                        }

                        // Reset options filter textbox
                        filter.FilterText = filter.DefaultText;
                        filter.TextForeground = Brushes.Gray;

                        // Clear the SelectedOptions collection
                        filter.SelectedOptions.Clear();
                        if (filter.AvailableOperators != null && filter.AvailableOperators.Any())
                        {
                            filter.OperatorSelection = filter.AvailableOperators.First();
                        }
                        break;


                    case FilterType.Numeric:
                        filter.SelectedNumericValue = null;
                        if (filter.AvailableOperators != null && filter.AvailableOperators.Any())
                        {
                            filter.OperatorSelection = filter.AvailableOperators.First();
                        }
                        // For filters that use checkboxes (e.g. CardsForTrade),
                        // explicitly reset the trade-related properties.
                        if (filter.CriteriaKey == "CardsForTrade")
                        {
                            filter.IsTradeChecked = false;
                            filter.IsNotTradeChecked = false;
                        }
                        if (filter.CriteriaKey == "ManaValue")
                        {
                            filter.OperatorSelection = OperatorType.GREATER_THAN;
                        }
                        break;
                }
            }
        }

        // Debug
        public virtual void DebugFullFilterState()
        {
            UpdateFilterSummary();
            Debug.WriteLine("===== DEBUG: FULL FILTER STATE =====");

            foreach (var filter in Filters.Values)
            {
                switch (filter.FilterCategory)
                {
                    case FilterType.Single:
                        // Only output if a non-empty single selection is made.
                        if (!string.IsNullOrWhiteSpace(filter.SelectedSingleOption) && filter.SelectedSingleOption != filter.DefaultText)
                        {
                            Debug.WriteLine($"Criteria: {filter.CriteriaKey} | Type: {filter.FilterCategory}");
                            Debug.WriteLine($"  Selected Value: {filter.SelectedSingleOption}");
                            Debug.WriteLine("----------------------");
                        }
                        break;

                    case FilterType.Multi:
                        // Only output if there is at least one selected option.
                        if (filter.SelectedOptions != null && filter.SelectedOptions.Any())
                        {
                            Debug.WriteLine($"Criteria: {filter.CriteriaKey} | Type: {filter.FilterCategory}");
                            Debug.WriteLine($"  Selected Options: {string.Join(", ", filter.SelectedOptions)}");
                            Debug.WriteLine($"  Operator: {filter.OperatorSelection}");
                            Debug.WriteLine("----------------------");
                        }
                        break;

                    case FilterType.Numeric:
                        // Only output if a numeric value is selected.
                        if (filter.SelectedNumericValue != null)
                        {
                            Debug.WriteLine($"Criteria: {filter.CriteriaKey} | Type: {filter.FilterCategory}");
                            Debug.WriteLine($"  Selected Numeric Value: {filter.SelectedNumericValue}");
                            Debug.WriteLine($"  Operator: {filter.OperatorSelection}");
                            Debug.WriteLine("----------------------");
                        }
                        break;
                }
            }

            Debug.WriteLine("===================================");
        }

    }

}

