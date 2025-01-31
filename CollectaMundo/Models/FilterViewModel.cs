using System.ComponentModel;
using System.Text;
using static CollectaMundo.MainWindow;

namespace CollectaMundo.Models
{
    public class FilterViewModel : INotifyPropertyChanged
    {
        private string _filterSummary = string.Empty;

        public string FilterSummary
        {
            get => _filterSummary;
            set
            {
                _filterSummary = value;
                OnPropertyChanged(nameof(FilterSummary)); // Notify UI of changes
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Ensure `UpdateSummary` updates the UI
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


