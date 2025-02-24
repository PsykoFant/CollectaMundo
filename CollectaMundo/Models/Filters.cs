using CollectaMundo.Utilities;
using System.ComponentModel;
using System.Diagnostics;
using static CollectaMundo.MainWindow;

namespace CollectaMundo.Models
{
    // Base class for all filters with common properties.
    public abstract class Filters
    {
        public required string CriteriaKey { get; set; }
    }

    // Selection-based filter, used during filtering operations.
    public class FilterSelections : Filters, INotifyPropertyChanged
    {
        private OperatorType _operator = OperatorType.OR;
        public OperatorType Operator
        {
            get => _operator;
            set
            {
                _operator = value;
                OnPropertyChanged(nameof(Operator));
            }
        }

        private string? _singleCriteria;
        public string? SingleCriteria
        {
            get => _singleCriteria;
            set
            {
                _singleCriteria = value;
                OnPropertyChanged(nameof(SingleCriteria));
            }
        }

        private HashSet<string> _multipleCriteria = [];
        public HashSet<string> MultipleCriteria
        {
            get => _multipleCriteria;
            set
            {
                _multipleCriteria = value;
                OnPropertyChanged(nameof(MultipleCriteria));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        public BaseFilterCriteria ToFilterCriteria()
        {
            return new StringFilterCriteria
            {
                CriteriaKey = this.CriteriaKey,
                SingleValue = this.SingleCriteria,
                MultipleValues = new HashSet<string>(this.MultipleCriteria),
                OperatorType = this.Operator,

                // PropertySelector is required, so we set it dynamically
                PropertySelector = card => card.GetType().GetProperty(CriteriaKey)?.GetValue(card)?.ToString()
            };
        }
    }

    // Default values and options for a filter.
    public class FilterDefaults : Filters, INotifyPropertyChanged
    {
        //public string CriteriaKey { get; set; } = string.Empty;
        public List<string> AllCriteria { get; set; } = [];

        private string _defaultText = string.Empty;
        public string DefaultText
        {
            get => _defaultText;
            set
            {
                _defaultText = value;
                OnPropertyChanged(nameof(DefaultText)); // Notify UI of updates
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // Base class for strongly-typed filter criteria.
    public abstract class BaseFilterCriteria : Filters
    {
        public OperatorType OperatorType { get; set; }

        /// <summary>
        /// Abstract method to check if a card matches the filter.
        /// </summary>
        public abstract bool Matches(CardSet card);
    }

    // Strongly-typed filter for string properties.    
    public class StringFilterCriteria : BaseFilterCriteria
    {
        public required Func<CardSet, string?> PropertySelector { get; set; }
        public string? SingleValue { get; set; }
        public HashSet<string>? MultipleValues { get; set; }

        public override bool Matches(CardSet card)
        {
            var propertyValue = PropertySelector(card);

            // Check single value criteria
            if (!string.IsNullOrWhiteSpace(SingleValue) &&
                (propertyValue?.Contains(SingleValue, StringComparison.OrdinalIgnoreCase) != true))
            {
                return false;
            }

            // Check multiple values criteria
            if (MultipleValues != null && MultipleValues.Count > 0)
            {
                return OperatorType switch
                {
                    OperatorType.OR => MultipleValues.Any(mv => propertyValue?.Contains(mv, StringComparison.OrdinalIgnoreCase) == true),
                    OperatorType.AND => MultipleValues.All(mv => propertyValue?.Contains(mv, StringComparison.OrdinalIgnoreCase) == true),
                    OperatorType.NOT => !MultipleValues.Any(mv => propertyValue?.Contains(mv, StringComparison.OrdinalIgnoreCase) == true),
                    _ => true
                };
            }

            return true;
        }
    }

    // Strongly-typed filter for numeric properties.
    public class NumericFilterCriteria : BaseFilterCriteria
    {
        public required Func<CardSet, double?> PropertySelector { get; set; }
        public double Value { get; set; }

        public override bool Matches(CardSet card)
        {
            var propertyValue = PropertySelector(card);

            // Check numeric criteria based on operator type
            if (!propertyValue.HasValue)
            {
                return false;
            }

            return OperatorType switch
            {
                OperatorType.LESS_THAN => propertyValue < Value,
                OperatorType.LESS_THAN_OR_EQUALS => propertyValue <= Value,
                OperatorType.GREATER_THAN => propertyValue > Value,
                OperatorType.GREATER_THAN_OR_EQUALS => propertyValue >= Value,
                OperatorType.EQUALS => Math.Abs(propertyValue.Value - Value) < 0.0001,
                OperatorType.NOT_EQUALS => Math.Abs(propertyValue.Value - Value) >= 0.0001,
                _ => false
            };
        }
    }
    public static class FilterManager
    {
        public static List<FilterDefaults> GetFilterDefaults(CardViewModel cardViewModel)
        {
            return [.. FilterCriteriaMappings.CriteriaMappings.Select(entry =>
            {
                var sourceCollection = entry.Value.Property == nameof(CardViewModel.allCards) ? cardViewModel.allCards : cardViewModel.myCards;
                var rawValues = ExtractCriteriaValues(entry.Key, sourceCollection);
                var removeItems = GetUnwantedItems(entry.Key);
                bool shouldNotSplit = entry.Value.ShouldNotSplit;
                var filteredValues = CleanAndFilter(rawValues, removeItems, shouldNotSplit);

                // Special handling for "Colors" (add predefined values)
                if (entry.Key == "Colors")
                {
                    var predefinedColors = new List<string> { "W", "U", "B", "R", "G", "C", "X", "Colorless" };
                    filteredValues = [.. predefinedColors.Union(filteredValues)];
                }

                return new FilterDefaults
                {
                    CriteriaKey = entry.Key,
                    AllCriteria = filteredValues,
                    DefaultText = $"{entry.Key} ..."
                };
            })];
        }

        private static List<string> ExtractCriteriaValues(string propertyName, List<CardSet> sourceCollection)
        {
            var propertyInfo = typeof(CardSet).GetProperty(propertyName);
            if (propertyInfo == null)
            {
                Debug.WriteLine($"[ERROR] Property '{propertyName}' not found in CardSet.");
                return [];
            }

            return [.. sourceCollection
                .Select(card => propertyInfo.GetValue(card))
                .Where(value => value != null)
                .SelectMany(value =>
                {
                    return value switch
                    {
                        string str => [str],                     // String properties
                        List<string> strList => strList,         // List<string> properties
                        double dbl => [dbl.ToString()],          // Convert numeric values to strings
                        int num => [num.ToString()],             // Convert int values to strings
                        _ => []                                  // Ignore unsupported types
                    };
                }).Distinct()];
        }
        private static List<string> CleanAndFilter(IEnumerable<string> input, HashSet<string>? removeItems, bool shouldNotSplit)
        {
            char[] separatorArray = [','];

            var processedItems = input
                .Where(item => !string.IsNullOrEmpty(item)) // Ensure we don't process null/empty values
                .SelectMany<string, string>(item => shouldNotSplit
                    ? new List<string> { item } // Keep whole string if shouldNotSplit == true
                    : item.Split(separatorArray, StringSplitOptions.RemoveEmptyEntries)) // Otherwise, split by comma
                .Select(item => item.Trim())
                .Where(item => removeItems == null || !removeItems.Contains(item)) // Apply filter
                .Distinct()
                .ToList();

            // ✅ Separate Numeric & Non-Numeric Values
            var numericValues = processedItems
                .Where(v => double.TryParse(v, out _))    // Extract numbers
                .Select(double.Parse)                     // Convert to actual numbers
                .OrderBy(n => n)                          // Sort Numerically
                .Select(n => n.ToString())                // Convert back to string
                .ToList();

            var stringValues = processedItems
                .Where(v => !double.TryParse(v, out _))   // Extract non-numeric values
                .OrderBy(v => v)                          // Sort Alphabetically
                .ToList();

            // Combine Numeric & String Values (Numeric First)
            return [.. numericValues, .. stringValues];
        }
        private static HashSet<string>? GetUnwantedItems(string criteriaKey)
        {
            return criteriaKey switch
            {
                "Types" => ["Eaturecray", "Summon", "Scariest", "You'll", "Ever", "See", "Jaguar", "Dragon", "Knights", "Legend", "instant", "Cards"],
                "SubTypes" => ["(creature", "and/or", "type)|Judge", "The"],
                _ => null
            };
        }
        public static IEnumerable<BaseFilterCriteria> GetActiveFilters(IEnumerable<FilterSelections> filterSelections)
        {
            return filterSelections
                .Where(selection => selection.MultipleCriteria.Count > 0 || !string.IsNullOrWhiteSpace(selection.SingleCriteria))
                .Select(selection => selection.ToFilterCriteria())
                .ToList();
        }
    }

}

