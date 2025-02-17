using CollectaMundo.Utilities;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;

namespace CollectaMundo.Models
{
    public class FilterViewModel
    {
        public ObservableCollection<FilterItemViewModel> Filters { get; } = new();
        private readonly CardViewModel _cardViewModel;

        public FilterViewModel(CardViewModel cardViewModel)
        {
            _cardViewModel = cardViewModel;
            PopulateFilters();
        }

        private void PopulateFilters()
        {
            foreach (var criteriaKey in FilterCriteriaMappings.CriteriaKeyToPropertyMap.Keys)
            {
                var filterItem = new FilterItemViewModel(criteriaKey);

                // Retrieve distinct values from CardSet dynamically
                var options = GetDistinctValuesForCriteria(criteriaKey);
                foreach (var option in options)
                {
                    filterItem.AvailableOptions.Add(option);
                }

                Filters.Add(filterItem);
            }
        }

        // Uses reflection to retrieve distinct values for a filter criteria (e.g., "Rarity", "Colors")
        private List<string> GetDistinctValuesForCriteria(string criteriaKey)
        {
            if (!FilterCriteriaMappings.CriteriaKeyToPropertyMap.TryGetValue(criteriaKey, out var propertyName))
            {
                Debug.WriteLine($"[ERROR] No property mapping found for {criteriaKey}");
                return new List<string>();
            }

            // Try to find the property in CardSet
            PropertyInfo? propertyInfo = typeof(CardSet).GetProperty(propertyName);
            if (propertyInfo == null)
            {
                Debug.WriteLine($"[ERROR] Property '{propertyName}' not found in CardSet.");
                return new List<string>();
            }

            return _cardViewModel.allCards
                .Select(card => propertyInfo.GetValue(card)?.ToString())
                .Where(value => !string.IsNullOrEmpty(value))
                .Distinct()
                .OrderBy(value => value)
                .ToList()!;
        }

        public FilterItemViewModel? GetFilterItem(string criteriaKey) =>
            Filters.FirstOrDefault(f => f.CriteriaKey == criteriaKey);

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
