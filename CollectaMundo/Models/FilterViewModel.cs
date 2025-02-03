using CollectaMundo.Utilities;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using static CollectaMundo.MainWindow;
using static CollectaMundo.Models.CardSet;

namespace CollectaMundo.Models
{
    public class FilterViewModel : INotifyPropertyChanged
    {
        private readonly CardViewModel _cardViewModel;

        public FilterViewModel(CardViewModel cardViewModel)
        {
            _cardViewModel = cardViewModel;
        }

        // Access mapping from the static class
        public Dictionary<string, string> CriteriaKeyToPropertyMap => FilterCriteriaMappings.CriteriaKeyToPropertyMap;

        private string _filterSummary = string.Empty;
        private string _allCardsCount = string.Empty;
        private string _myCollectionCount = string.Empty;

        public ObservableCollection<FilterDefaults> FilterDefaults { get; set; } = new();

        public string FilterSummary
        {
            get => _filterSummary;
            set
            {
                _filterSummary = value;
                OnPropertyChanged(nameof(FilterSummary));
            }
        }
        public string AllCardsCount
        {
            get => _allCardsCount;
            set
            {
                _allCardsCount = value;
                OnPropertyChanged(nameof(AllCardsCount));
            }
        }
        public string MyCollectionCount
        {
            get => _myCollectionCount;
            set
            {
                _myCollectionCount = value;
                OnPropertyChanged(nameof(MyCollectionCount));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public void UpdateCardCount(string datagridName, int count)
        {
            if (datagridName == "AllCardsDataGrid")
            {
                AllCardsCount = $"Showing: {count} cards out of total {MainWindow.CurrentInstance.allCards.Count} cards.";
            }
            else if (datagridName == "MyCollectionDataGrid")
            {
                MyCollectionCount = $"Showing: {count} cards out of total {MainWindow.CurrentInstance.myCards.Count} cards in your collection.";
            }
        }

        // `UpdateSummary` updates the UI
        public void UpdateSummary(IEnumerable<BaseFilterCriteria> filterCriteria)
        {
            var summary = new StringBuilder();

            foreach (var filter in filterCriteria)
            {
                if (filter is StringFilterCriteria stringFilter)
                {
                    if (!string.IsNullOrWhiteSpace(stringFilter.SingleValue))
                    {
                        summary.Append($"{filter.CriteriaKey}: \"{stringFilter.SingleValue}\" AND ");
                    }

                    if (stringFilter.MultipleValues is { Count: > 0 })
                    {
                        string operatorSymbol = stringFilter.OperatorType switch
                        {
                            OperatorType.OR => "OR",
                            OperatorType.AND => "AND",
                            OperatorType.NOT => "NOT",
                            _ => ""
                        };

                        var filterSegment = stringFilter.OperatorType == OperatorType.NOT
                            ? string.Join(", ", stringFilter.MultipleValues.Select(mv => $"NOT {mv}"))
                            : string.Join($" {operatorSymbol} ", stringFilter.MultipleValues);

                        summary.Append($"{filter.CriteriaKey}: {{{filterSegment}}} AND ");
                    }
                }
                else if (filter is NumericFilterCriteria numericFilter)
                {
                    string numericOperator = numericFilter.OperatorType switch
                    {
                        OperatorType.LESS_THAN => "<",
                        OperatorType.LESS_THAN_OR_EQUALS => "<=",
                        OperatorType.GREATER_THAN => ">",
                        OperatorType.GREATER_THAN_OR_EQUALS => ">=",
                        OperatorType.EQUALS => "==",
                        OperatorType.NOT_EQUALS => "!=",
                        _ => ""
                    };

                    summary.Append($"{filter.CriteriaKey} {numericOperator} {numericFilter.Value} AND ");
                }
            }

            if (summary.Length > 5)
            {
                summary.Remove(summary.Length - 5, 5);
            }

            // This updates the UI
            FilterSummary = summary.ToString();
        }

        public void PopulateFilterUiElements()
        {
            List<string> allColors = ["W", "U", "B", "R", "G", "C", "X", "Colorless"];
            HashSet<string> typesToRemove = ["Eaturecray", "Summon", "Scariest", "You'll", "Ever", "See", "Jaguar", "Dragon", "Knights", "Legend", "instant", "Cards"];
            HashSet<string> subTypesToRemove = ["(creature", "and/or", "type)|Judge", "The"];

            FilterDefaults.Clear();

            var criteriaKeys = CriteriaKeyToPropertyMap.Keys.ToList();

            foreach (var criteriaKey in criteriaKeys)
            {
                var filter = new FilterDefaults { CriteriaKey = criteriaKey };
                var propertyInfo = typeof(CardSet).GetProperty(criteriaKey)
                                 ?? typeof(CardInCollection).GetProperty(criteriaKey)
                                 ?? typeof(CardInDeck).GetProperty(criteriaKey);

                if (propertyInfo == null)
                {
                    Debug.WriteLine($"Property '{criteriaKey}' not found on any supported types.");
                    filter.AllCriteria = new List<string>();
                    FilterDefaults.Add(filter);
                    continue;
                }

                HashSet<string>? removeItems = criteriaKey switch
                {
                    "Types" => typesToRemove,
                    "SubTypes" => subTypesToRemove,
                    _ => null
                };

                var allCards = _cardViewModel.allCards;
                var dynamicCriteria = CleanAndFilter(
                    allCards.Where(card => propertyInfo.DeclaringType?.IsInstanceOfType(card) == true)
                            .Select(card => propertyInfo.GetValue(card)?.ToString()),
                    removeItems
                );

                filter.AllCriteria = criteriaKey == "Colors" ? allColors : dynamicCriteria.ToList();
                FilterDefaults.Add(filter);
            }

            OnPropertyChanged(nameof(FilterDefaults));
        }

        private IEnumerable<string> CleanAndFilter(IEnumerable<string?> input, HashSet<string>? removeItems = null)
        {
            char[] separatorArray = [','];

            return input
                .Where(item => !string.IsNullOrEmpty(item))
                .SelectMany(item => item!.Split(separatorArray, StringSplitOptions.RemoveEmptyEntries))
                .Select(item => item.Trim())
                .Where(item => removeItems == null || !removeItems.Contains(item))
                .Distinct()
                .OrderBy(item => item);
        }
    }


}


