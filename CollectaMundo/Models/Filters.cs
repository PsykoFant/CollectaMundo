using System.ComponentModel;
using static CollectaMundo.MainWindow;

namespace CollectaMundo.Models
{
    /// <summary>
    /// Base class for all filters with common properties.
    /// </summary>
    public abstract class Filters
    {
        public required string CriteriaKey { get; set; }
    }

    /// <summary>
    /// Selection-based filter, used during filtering operations.
    /// </summary>
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

        private HashSet<string> _multipleCriteria = new();
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
    }


    /// <summary>
    /// Default values and options for a filter.
    /// </summary>
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

    /// <summary>
    /// Base class for strongly-typed filter criteria.
    /// </summary>
    public abstract class BaseFilterCriteria : Filters
    {
        public OperatorType OperatorType { get; set; }

        /// <summary>
        /// Abstract method to check if a card matches the filter.
        /// </summary>
        public abstract bool Matches(CardSet card);
    }

    /// <summary>
    /// Strongly-typed filter for string properties.
    /// </summary>
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

    /// <summary>
    /// Strongly-typed filter for numeric properties.
    /// </summary>
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
}

