using CollectaMundo.Utilities;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Windows.Input;
using static CollectaMundo.MainWindow;
using static CollectaMundo.Models.CardSet;

namespace CollectaMundo.Models
{
    public class FilterViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private readonly CardViewModel _cardViewModel;
        public ICommand SetSelectedCriteriaCommand { get; }

        public FilterViewModel(CardViewModel cardViewModel)
        {
            _cardViewModel = cardViewModel;

            // Initialize filter defaults
            PopulateFilterDefaults();

            //Set default SelectedCriteriaKey to the first available filter key
            SelectedCriteriaKey = FilterDefaults.FirstOrDefault()?.CriteriaKey ?? string.Empty;
        }

        private bool _isDropDownOpen;
        public bool IsDropDownOpen
        {
            get => _isDropDownOpen;
            set
            {
                _isDropDownOpen = value;
                OnPropertyChanged(nameof(IsDropDownOpen));
            }
        }


        private string _defaultText;
        public string DefaultText
        {
            get => _defaultText;
            set
            {
                if (_defaultText != value)
                {
                    _defaultText = value;
                    OnPropertyChanged(nameof(DefaultText));
                }
            }
        }

        private string _selectedCriteriaKey;
        public string SelectedCriteriaKey
        {
            get => _selectedCriteriaKey;
            set
            {
                if (_selectedCriteriaKey != value)
                {
                    _selectedCriteriaKey = value;

                    // Set the DefaultText based on the selected CriteriaKey
                    var filter = FilterDefaults.FirstOrDefault(fd => fd.CriteriaKey == _selectedCriteriaKey);
                    DefaultText = filter?.DefaultText ?? $"Filter {_selectedCriteriaKey} ...";

                    OnPropertyChanged(nameof(SelectedCriteriaKey));
                    OnPropertyChanged(nameof(DefaultText));
                    OnPropertyChanged(nameof(FilteredListBoxItems)); // Update the listbox items
                }
            }
        }


        private string _filterText = string.Empty;
        public string FilterText
        {
            get => _filterText;
            set
            {
                if (_filterText != value)
                {
                    _filterText = value;
                    OnPropertyChanged(nameof(FilterText));
                    OnPropertyChanged(nameof(FilteredListBoxItems)); // Update ListBox when text changes
                }
            }
        }
        public ObservableCollection<string> FilteredListBoxItems
        {
            get
            {
                var filter = FilterDefaults.FirstOrDefault(fd => fd.CriteriaKey == SelectedCriteriaKey);
                if (filter == null) return new ObservableCollection<string>();

                // Filter items based on text input
                var filteredItems = string.IsNullOrWhiteSpace(FilterText)
                    ? filter.AllCriteria
                    : filter.AllCriteria.Where(item => item.IndexOf(FilterText, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

                return new ObservableCollection<string>(filteredItems);
            }
        }

        // Access mapping from the static class
        public Dictionary<string, string> CriteriaKeyToPropertyMap => FilterCriteriaMappings.CriteriaKeyToPropertyMap;

        private string _filterSummary = string.Empty;
        private string _allCardsCount = string.Empty;
        private string _myCollectionCount = string.Empty;

        public ObservableCollection<FilterSelections> FilterSelections { get; set; } = new();
        public ObservableCollection<FilterDefaults> FilterDefaults { get; set; } = new();

        public void PopulateFilterDefaults()
        {
            FilterDefaults.Clear(); // Ensure a fresh start

            foreach (var criteriaKey in FilterCriteriaMappings.CriteriaKeyToPropertyMap.Keys)
            {
                var filter = new FilterDefaults { CriteriaKey = criteriaKey };

                // Use reflection to dynamically find matching properties
                var propertyName = FilterCriteriaMappings.CriteriaKeyToPropertyMap[criteriaKey];
                var propertyInfo = typeof(CardSet).GetProperty(propertyName)
                                 ?? typeof(CardInCollection).GetProperty(propertyName)
                                 ?? typeof(CardInDeck).GetProperty(propertyName);

                if (propertyInfo != null)
                {
                    // Dynamically determine items to remove (e.g., invalid types/subtypes)
                    HashSet<string>? removeItems = criteriaKey switch
                    {
                        "Types" => new() { "Eaturecray", "Summon", "Scariest", "You'll", "Ever", "See", "Jaguar", "Dragon", "Knights", "Legend", "instant", "Cards" },
                        "SubTypes" => new() { "(creature", "and/or", "type)|Judge", "The" },
                        _ => null
                    };

                    // Extract values & apply cleaning function
                    var values = CleanAndFilter(
                        _cardViewModel.AllCardsView
                            .Cast<CardSet>()
                            .Where(card => propertyInfo.DeclaringType!.IsInstanceOfType(card)) // Ensure compatibility
                            .Select(card => propertyInfo.GetValue(card)?.ToString()),
                        removeItems
                    ).ToList();

                    filter.AllCriteria = values;
                }
                else
                {
                    Debug.WriteLine($"Property '{criteriaKey}' not found on any supported types.");
                    filter.AllCriteria = [];
                }

                filter.DefaultText = $"Filter {criteriaKey} ...";

                // Add to ObservableCollection so UI updates automatically
                FilterDefaults.Add(filter);
            }
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
    }
}
