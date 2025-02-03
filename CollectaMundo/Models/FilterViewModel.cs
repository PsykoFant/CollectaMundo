using CollectaMundo.Utilities;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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

        public List<FilterSelections> FilterSelections { get; set; } = new();
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

            // Force UI refresh by notifying PropertyChanged
            OnPropertyChanged(nameof(FilterDefaults));

            Debug.WriteLine("FilterDefaults after PopulateFilterDefaults():");
            foreach (var filter in FilterDefaults)
            {
                Debug.WriteLine($"CriteriaKey: {filter.CriteriaKey}, DefaultText: {filter.DefaultText}");
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

        private void SetDefaultText()
        {
            foreach (var filter in FilterDefaults)
            {
                // Generate the default text
                filter.DefaultText = $"Filter {filter.CriteriaKey} ...";

                Application.Current.Dispatcher.Invoke(() =>
                {
                    // Dynamically retrieve UI elements based on CriteriaKey
                    string comboBoxName = $"{filter.CriteriaKey}ComboBox";
                    string textBoxName = $"Filter{filter.CriteriaKey}TextBox";

                    // Find the ComboBox by name
                    if (Application.Current.MainWindow?.FindName(comboBoxName) is ComboBox comboBox)
                    {
                        // Find the TextBox inside the ComboBox template
                        if (comboBox.Template.FindName(textBoxName, comboBox) is TextBox filterTextBox)
                        {
                            // Set the default text and style
                            filterTextBox.Text = filter.DefaultText ?? "Whoops, something went wrong!";
                            filterTextBox.Foreground = new SolidColorBrush(Colors.Gray);
                        }
                    }
                    else if (Application.Current.MainWindow?.FindName(textBoxName) is TextBox textBox) // Directly locate the TextBox
                    {
                        // Set the default text and style
                        textBox.Text = filter.DefaultText ?? "Whoops, something went wrong!";
                        textBox.Foreground = new SolidColorBrush(Colors.Gray);
                    }
                });
            }
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



    }


}


