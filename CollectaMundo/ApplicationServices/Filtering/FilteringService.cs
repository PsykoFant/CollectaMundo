using CollectaMundo.DomainLogic.Filtering;
using CollectaMundo.DomainLogic.Filtering.Enums;
using CollectaMundo.ViewModels.Filtering;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace CollectaMundo.ApplicationServices.Filtering
{
    public class FilteringService : IFilteringService
    {
        public static bool HasActiveFilters(IEnumerable<FilterItemViewModel> filters)
        {
            return filters.Any(f =>
                f.FilterCategory switch
                {
                    FilterType.Single =>
                        !string.IsNullOrWhiteSpace(f.SelectedSingleOption) &&
                        f.SelectedSingleOption != f.DefaultText,

                    FilterType.Multi =>
                        f.SelectedOptions.Count > 0,

                    FilterType.Numeric =>
                        f.SelectedNumericValue is not null,

                    _ => false
                });
        }
        public List<TCard> ApplyFilters<TCard>(IEnumerable<TCard> cards, IEnumerable<FilterItemViewModel> vmFilters)
        {
            if (vmFilters == null || !vmFilters.Any())
            {
                return [.. cards];
            }

            var filtered = cards;

            var criteria = vmFilters
                .Select(vm => new FilteringLogic<TCard>(
                    vm.CriteriaKey,
                    vm.FilterCategory,
                    vm.SelectedOptions,
                    vm.SelectedSingleOption,
                    vm.SelectedNumericValue,
                    vm.OperatorSelection,
                    vm.DefaultText))
                .ToList();

            if (criteria.Count == 0)
            {
                return [.. filtered];
            }

            try
            {
                return [.. filtered.Where(card => criteria.All(c => c.Matches(card)))];
            }
            catch (Exception ex)
            {

                Debug.WriteLine($"[Filter] ERROR during filtering: {ex}");
                return [.. cards]; // fallback
            }
        }
        public void ResetAllFilters(IEnumerable<FilterItemViewModel> allFilters)
        {
            var filters = allFilters.ToList();

            foreach (var filter in filters)
            {
                filter.BeginBulkUpdate();
            }

            try
            {
                foreach (var filter in filters)
                {
                    ResetFilter(filter);
                }
            }
            finally
            {
                foreach (var filter in filters)
                {
                    filter.EndBulkUpdate(notifyFilterChanged: false);
                }
            }
        }
        private static void ResetFilter(FilterItemViewModel filter)
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
                    if (filter.CriteriaKey == "GameplayCard")
                    {
                        filter.IsGameplayCardsOnlyChecked = false;
                    }

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
        public string BuildSummary(IEnumerable<FilterItemViewModel> filters)
        {
            try
            {
                var summary = new StringBuilder();

                foreach (var filter in filters)
                {
                    switch (filter.FilterCategory)
                    {
                        case FilterType.Single:
                            if (!string.IsNullOrWhiteSpace(filter.SelectedSingleOption) && filter.SelectedSingleOption != filter.DefaultText)
                            {
                                summary.Append($"{filter.ReadableLabel}: \"{filter.SelectedSingleOption}\" AND ");
                            }
                            break;

                        case FilterType.Multi:
                            if (filter.SelectedOptions != null && filter.SelectedOptions.Any())
                            {
                                string operatorSymbol = filter.OperatorSelection switch
                                {
                                    OperatorType.OR => "OR",
                                    OperatorType.AND => "AND",
                                    OperatorType.NOT => "AND",
                                    _ => ""
                                };

                                var selectedDisplayNames = filter.SelectedOptionDisplayNames;

                                string filterSegment = filter.OperatorSelection == OperatorType.NOT
                                    ? string.Join($" {operatorSymbol} ", selectedDisplayNames.Select(opt => $"NOT {opt}"))
                                    : string.Join($" {operatorSymbol} ", selectedDisplayNames);

                                summary.Append($"{filter.ReadableLabel}: {{{filterSegment}}} AND ");
                            }

                            break;

                        case FilterType.Numeric:
                            if (filter.SelectedNumericValue is not null)
                            {
                                if (filter.CriteriaKey.Equals("GamePlayCard", StringComparison.OrdinalIgnoreCase))
                                {
                                    summary.Append($"{filter.ReadableLabel} == true AND ");
                                }
                                else
                                {
                                    summary.Append(
                                        $"{filter.ReadableLabel} " +
                                        $"{GetOperatorSymbol(filter.OperatorSelection)} " +
                                        $"{filter.SelectedNumericValue} AND ");
                                }
                            }

                            break;
                    }
                }

                // Remove the trailing " AND " if present.
                if (summary.Length >= 5)
                {
                    summary.Remove(summary.Length - 5, 5);
                }

                return summary.ToString();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating filter summary: {ex.Message}");
                MessageBox.Show($"Error updating filter summary: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return string.Empty;
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
    }
}
