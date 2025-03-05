using CollectaMundo.Utilities;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Windows.Data;
using System.Windows.Input;
using static CollectaMundo.MainWindow;

namespace CollectaMundo.Models
{
    public class FilterViewModel : INotifyPropertyChanged
    {
        public Dictionary<string, FilterItemViewModel> Filters { get; } = [];

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        // ICommand to clear filters
        public ICommand? ClearFiltersCommand { get; }

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

        private readonly CardViewModel _cardViewModel;
        public FilterViewModel(CardViewModel cardViewModel)
        {
            _cardViewModel = cardViewModel ?? throw new ArgumentNullException(nameof(cardViewModel));

            var filterDefaults = FilterManager.GetFilterDefaults(cardViewModel);
            foreach (var filter in filterDefaults)
            {
                Filters[filter.CriteriaKey] = new FilterItemViewModel(
                    filter.CriteriaKey,
                    filter.FilterOptions,
                    filter.DefaultText,
                    this,                  // Passing the FilterViewModel as the source of truth
                    filter.NumericCriteria // Pass numeric criteria if applicable
                );
            }

            // Initialize the command using the ClearFilters method.
            ClearFiltersCommand = new RelayCommand(ClearFilters);
        }

        // Applies the current filter criteria to the provided ListCollectionView.
        private void ApplyFilterToView(ListCollectionView view)
        {
            view.Filter = item =>
            {
                if (item is CardSet card)
                {
                    // Only include the card if it satisfies all active filters.
                    return Filters.Values.All(filter => filter.Matches(card));
                }
                return false;
            };

            view.Refresh();
        }

        public void ApplyFiltering()
        {
            ApplyFilterToView(_cardViewModel.AllCardsView);
            ApplyFilterToView(_cardViewModel.MyCollectionView);
            ApplyFilterToView(_cardViewModel.AllCardsForDecksView);
            UpdateFilterSummary();
        }

        // This method aggregates the current filter selections into a summary string.
        private void UpdateFilterSummary()
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

        // Helper method to convert operator enum to a symbol.
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
        public void ClearFilters()
        {
            foreach (var filter in Filters.Values)
            {
                switch (filter.FilterCategory)
                {
                    case FilterType.Single:
                        filter.SelectedSingleOption = null;
                        filter.FreetextSearch = filter.DefaultText;
                        filter.FilterText = filter.DefaultText;
                        break;

                    case FilterType.Multi:
                        // Uncheck each option so that the UI updates and SelectedOptions is recalculated.
                        foreach (var option in filter.FilterOptions)
                        {
                            option.IsSelected = false;
                        }
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
                        break;
                }
            }
            //UpdateFilterSummary();
        }



        // Debug
        public void DebugFullFilterState()
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

//public void UpdateCardCount(string datagridName, int count)
//{
//    if (datagridName == "AllCardsDataGrid")
//    {
//        AllCardsCount = $"Showing: {count} cards out of total {MainWindow.CurrentInstance.allCards.Count} cards.";
//    }
//    else if (datagridName == "MyCollectionDataGrid")
//    {
//        MyCollectionCount = $"Showing: {count} cards out of total {MainWindow.CurrentInstance.myCollection.Count} cards in your collection.";
//    }
//}

