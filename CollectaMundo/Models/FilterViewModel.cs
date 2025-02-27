using CollectaMundo.Utilities;
using System.ComponentModel;
using System.Diagnostics;

namespace CollectaMundo.Models
{
    public class FilterViewModel : INotifyPropertyChanged
    {
        public Dictionary<string, FilterItemViewModel> Filters { get; } = [];

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        public FilterViewModel(CardViewModel cardViewModel)
        {
            var filterDefaults = FilterManager.GetFilterDefaults(cardViewModel);
            foreach (var filter in filterDefaults)
            {
                // ✅ Pass `FilterOptions` instead of `AllCriteria`
                Filters[filter.CriteriaKey] = new FilterItemViewModel(filter.CriteriaKey, filter.FilterOptions, filter.DefaultText);
            }
        }

        // Debug
        public FilterItemViewModel? GetFilterItem(string criteriaKey) => Filters.TryGetValue(criteriaKey, out var filterItem) ? filterItem : null;
        public void DebugFilterItems(string criteriaKey)
        {
            var filter = GetFilterItem(criteriaKey);
            if (filter != null)
            {
                filter.DebugFilterItem();
            }
            else
            {
                Debug.WriteLine($"[DEBUG]: No filter found for {criteriaKey}");
            }
        }

        public void DebugFullFilterState()
        {
            Debug.WriteLine("===== DEBUG: FULL FILTER STATE =====");

            foreach (var filter in Filters.Values)
            {
                Debug.WriteLine($"Criteria: {filter.CriteriaKey} | Type: {filter.FilterCategory}");

                switch (filter.FilterCategory)
                {
                    case FilterType.Single:
                        Debug.WriteLine($"  Selected Value: {filter.SelectedSingleOption}");
                        break;

                        //case FilterType.Multi:
                        //    Debug.WriteLine($"  Selected Options: {string.Join(", ", filter.SelectedOptions)}");
                        //    Debug.WriteLine($"  Operator: {filter.OperatorSelection}");
                        //    break;

                        //case FilterType.Numeric:
                        //    Debug.WriteLine($"  Selected Numeric Value: {filter.SelectedNumericValue}");
                        //    Debug.WriteLine($"  Operator: {filter.OperatorSelection}");
                        //    break;
                }

                Debug.WriteLine("---------------------------------");
            }

            Debug.WriteLine("===================================");
        }
    }

}




//private void UpdateFilteredListBoxItems()
//{
//    var filter = FilterDefaults.FirstOrDefault(fd => fd.CriteriaKey == SelectedCriteriaKey);
//    if (filter == null || filter.AllCriteria.Count == 0)
//    {
//        _filteredListBoxItems.Clear();
//        return;
//    }

//    // Apply filtering
//    var filteredItems = string.IsNullOrWhiteSpace(FilterText)
//        ? filter.AllCriteria
//        : filter.AllCriteria.Where(item => item.IndexOf(FilterText, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

//    // 🔹 Make sure the UI updates immediately by resetting the collection
//    Application.Current.Dispatcher.Invoke(() =>
//    {
//        _filteredListBoxItems.Clear();
//        foreach (var item in filteredItems)
//        {
//            _filteredListBoxItems.Add(item);
//        }
//    }, System.Windows.Threading.DispatcherPriority.Render);

//    // 🔹 Explicitly notify UI
//    OnPropertyChanged(nameof(FilteredListBoxItems));
//}

//public void UpdateCardCount(string datagridName, int count)
//{
//    if (datagridName == "AllCardsDataGrid")
//    {
//        AllCardsCount = $"Showing: {count} cards out of total {MainWindow.CurrentInstance.allCards.Count} cards.";
//    }
//    else if (datagridName == "MyCollectionDataGrid")
//    {
//        MyCollectionCount = $"Showing: {count} cards out of total {MainWindow.CurrentInstance.myCards.Count} cards in your collection.";
//    }
//}

// `UpdateSummary` updates the UI
//public void UpdateSummary(IEnumerable<BaseFilterCriteria> filterCriteria)
//{
//    var summary = new StringBuilder();

//    foreach (var filter in filterCriteria)
//    {
//        if (filter is StringFilterCriteria stringFilter)
//        {
//            if (!string.IsNullOrWhiteSpace(stringFilter.SingleValue))
//            {
//                summary.Append($"{filter.CriteriaKey}: \"{stringFilter.SingleValue}\" AND ");
//            }

//            if (stringFilter.MultipleValues is { Count: > 0 })
//            {
//                string operatorSymbol = stringFilter.OperatorType switch
//                {
//                    OperatorType.OR => "OR",
//                    OperatorType.AND => "AND",
//                    OperatorType.NOT => "NOT",
//                    _ => ""
//                };

//                var filterSegment = stringFilter.OperatorType == OperatorType.NOT
//                    ? string.Join(", ", stringFilter.MultipleValues.Select(mv => $"NOT {mv}"))
//                    : string.Join($" {operatorSymbol} ", stringFilter.MultipleValues);

//                summary.Append($"{filter.CriteriaKey}: {{{filterSegment}}} AND ");
//            }
//        }
//        else if (filter is NumericFilterCriteria numericFilter)
//        {
//            string numericOperator = numericFilter.OperatorType switch
//            {
//                OperatorType.LESS_THAN => "<",
//                OperatorType.LESS_THAN_OR_EQUALS => "<=",
//                OperatorType.GREATER_THAN => ">",
//                OperatorType.GREATER_THAN_OR_EQUALS => ">=",
//                OperatorType.EQUALS => "==",
//                OperatorType.NOT_EQUALS => "!=",
//                _ => ""
//            };

//            summary.Append($"{filter.CriteriaKey} {numericOperator} {numericFilter.Value} AND ");
//        }
//    }

//    if (summary.Length > 5)
//    {
//        summary.Remove(summary.Length - 5, 5);
//    }

//    // This updates the UI
//    FilterSummary = summary.ToString();
//}
