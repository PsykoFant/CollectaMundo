using CollectaMundo.Utilities;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using static CollectaMundo.MainWindow;

namespace CollectaMundo.Models
{
    public class FilterItemViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        public string CriteriaKey { get; }

        public bool _suppressFiltering = false; // Used to temporarily disable filtering

        public ObservableCollection<string>? AvailableOptions { get; } // For text-based filters
        public ObservableCollection<int>? AvailableNumericOptions { get; } // For numeric filters
        public ObservableCollection<OperatorType>? AvailableOperators { get; }

        // Operator selection (e.g., "<", ">=", "=")
        private OperatorType _operatorSelection;
        public OperatorType OperatorSelection
        {
            get => _operatorSelection;
            set
            {
                if (_operatorSelection != value)
                {
                    _operatorSelection = value;
                    OnPropertyChanged(nameof(OperatorSelection));
                }
            }
        }

        // **Filter Text (for text-based filters)**
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

                    if (!_suppressFiltering)
                    {
                        UpdateFilteredOptions();
                    }
                }
            }
        }

        // **Selected Numeric Value (for numeric filters)**
        private int? _selectedNumericValue;
        public int? SelectedNumericValue
        {
            get => _selectedNumericValue;
            set
            {
                if (_selectedNumericValue != value)
                {
                    _selectedNumericValue = value;
                    OnPropertyChanged(nameof(SelectedNumericValue));
                }
            }
        }

        public string DefaultText { get; }

        private ObservableCollection<string> _filteredOptions;
        public ObservableCollection<string> FilteredOptions
        {
            get => _filteredOptions;
            private set
            {
                _filteredOptions = value;
                OnPropertyChanged(nameof(FilteredOptions));
            }
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

        public FilterItemViewModel(string criteriaKey, IEnumerable<string> availableOptions, string defaultText)
        {
            CriteriaKey = criteriaKey;
            AvailableOptions = new ObservableCollection<string>(availableOptions);
            DefaultText = defaultText;

            _filterText = DefaultText;
            _filteredOptions = new ObservableCollection<string>(availableOptions);

            if (FilterCriteriaMappings.CriteriaMappings.TryGetValue(criteriaKey, out var mapping))
            {
                AvailableOperators = new ObservableCollection<OperatorType>(mapping.Operators);
                OperatorSelection = mapping.Operators.FirstOrDefault(OperatorType.OR); // Default "OR"
            }
            else
            {
                AvailableOperators = new ObservableCollection<OperatorType> { OperatorType.OR }; // Default fallback
                OperatorSelection = OperatorType.OR;
            }
        }

        // **Constructor for Numeric Filters**
        public FilterItemViewModel(string criteriaKey, IEnumerable<int> availableNumericOptions, List<OperatorType> operators)
        {
            CriteriaKey = criteriaKey;
            AvailableNumericOptions = new ObservableCollection<int>(availableNumericOptions);
            AvailableOperators = new ObservableCollection<OperatorType>(operators);
            OperatorSelection = operators.FirstOrDefault(OperatorType.EQUALS); // Default "="
            SelectedNumericValue = null;
        }

        private void UpdateFilteredOptions()
        {
            var filtered = AvailableOptions?
                .Where(option => string.IsNullOrWhiteSpace(FilterText) ||
                                 option.IndexOf(FilterText, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            FilteredOptions = new ObservableCollection<string>(filtered ?? []);
        }

        public void DebugFilterItem()
        {
            Debug.WriteLine($"===== DEBUG: Filter Item ({CriteriaKey}) =====");
            Debug.WriteLine($"Default Text: {DefaultText}");
            Debug.WriteLine($"Filter Text: {FilterText}");
            Debug.WriteLine($"Available Options: {string.Join(", ", AvailableOptions ?? [])}");
            Debug.WriteLine($"Available Numeric Options: {string.Join(", ", AvailableNumericOptions ?? [])}");
            Debug.WriteLine($"Number of options: {AvailableOptions?.Count ?? 0}");
            Debug.WriteLine($"====================================");
        }
    }
}
