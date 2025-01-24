using System.Diagnostics;
using static CollectaMundo.MainWindow;
using static CollectaMundo.Models.CardSet;

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
    public class FilterSelections : Filters
    {
        public OperatorType Operator { get; set; } = OperatorType.OR;
        public string? SingleCriteria { get; set; } = null;
        public HashSet<string> MultipleCriteria { get; set; } = [];
        public double NumberCriteria { get; set; } = -1;

        /// <summary>
        /// Converts this FilterSelections into a strongly-typed BaseFilterCriteria.
        /// </summary>
        /// <returns>A BaseFilterCriteria object.</returns>
        public BaseFilterCriteria ToFilterCriteria()
        {
            if (NumberCriteria != -1)
            {
                return new NumericFilterCriteria
                {
                    CriteriaKey = CriteriaKey,
                    PropertySelector = ResolveNumericPropertySelector(CriteriaKey),
                    Value = NumberCriteria,
                    OperatorType = Operator
                };
            }

            return new StringFilterCriteria
            {
                CriteriaKey = CriteriaKey,
                PropertySelector = ResolveStringPropertySelector(CriteriaKey),
                SingleValue = SingleCriteria,
                MultipleValues = MultipleCriteria,
                OperatorType = Operator
            };
        }
        private static Func<CardSet, string?> ResolveStringPropertySelector(string criteriaKey)
        {
            var property = typeof(CardSet).GetProperty(criteriaKey)
                          ?? typeof(CardInCollection).GetProperty(criteriaKey)
                          ?? typeof(CardInDeck).GetProperty(criteriaKey);

            if (property == null)
            {
                throw new InvalidOperationException($"Property '{criteriaKey}' not found on any supported types.");
            }

            return card => property.GetValue(card) as string;
        }
        private static Func<CardSet, double?> ResolveNumericPropertySelector(string criteriaKey)
        {
            // Check if the property exists on CardSet or its subclasses
            var property = typeof(CardSet).GetProperty(criteriaKey)
                          ?? typeof(CardInCollection).GetProperty(criteriaKey)
                          ?? typeof(CardInDeck).GetProperty(criteriaKey);

            if (property == null || !IsNumericType(property.PropertyType))
            {
                Debug.WriteLine($"Numeric property '{criteriaKey}' not found or not of a numeric type.");
                return _ => null;
            }

            return card =>
            {
                try
                {
                    // Only access the property if the object type matches
                    if (property.DeclaringType != null && property.DeclaringType.IsInstanceOfType(card))
                    {
                        var value = property.GetValue(card);
                        return value != null ? Convert.ToDouble(value) : null;
                    }

                    // Skip if the card's type doesn't match the declaring type of the property
                    return null;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error accessing numeric property '{criteriaKey}': {ex.Message}");
                    return null;
                }
            };

            static bool IsNumericType(Type type)
            {
                return type == typeof(int) || type == typeof(double) || type == typeof(float) || type == typeof(long) || type == typeof(short);
            }
        }

    }

    /// <summary>
    /// Default values and options for a filter.
    /// </summary>
    public class FilterDefaults : Filters
    {
        public List<string> AllCriteria { get; set; } = [];
        public string? DefaultText { get; set; } = null;
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
            if (!propertyValue.HasValue) return false;

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

