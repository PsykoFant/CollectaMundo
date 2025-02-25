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
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public string CriteriaKey { get; }
        public bool _suppressFiltering = false; // Used to temporarily disable filtering

        // **List of options with selection state**
        public ObservableCollection<FilterOption> FilterOptions { get; }

        // **Selected values**
        public ObservableCollection<string> SelectedOptions { get; } = new();

        // **Operators**
        public ObservableCollection<OperatorType>? AvailableOperators { get; }

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

        // **Filter Text**
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

        public string DefaultText { get; }

        private ObservableCollection<FilterOption> _filteredOptions;
        public ObservableCollection<FilterOption> FilteredOptions
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

        // **Constructor**
        public FilterItemViewModel(string criteriaKey, IEnumerable<string> availableOptions, string defaultText)
        {
            CriteriaKey = criteriaKey;
            DefaultText = defaultText;
            _filterText = DefaultText;

            // Convert available options into FilterOption objects
            FilterOptions = new ObservableCollection<FilterOption>(
                availableOptions.Select(option => new FilterOption(option))
            );

            // Initially, filtered options match all options
            _filteredOptions = new ObservableCollection<FilterOption>(FilterOptions);

            // Subscribe to selection changes
            foreach (var filterOption in FilterOptions)
            {
                filterOption.PropertyChanged += (sender, e) =>
                {
                    if (e.PropertyName == nameof(FilterOption.IsSelected))
                    {
                        UpdateSelectedOptions();
                    }
                };
            }

            // Set available operators
            if (FilterCriteriaMappings.CriteriaMappings.TryGetValue(criteriaKey, out var mapping))
            {
                AvailableOperators = new ObservableCollection<OperatorType>(mapping.Operators);
                OperatorSelection = mapping.Operators.FirstOrDefault();
            }
        }

        // **Update selected options when checkboxes are checked/unchecked**
        private void UpdateSelectedOptions()
        {
            SelectedOptions.Clear();
            foreach (var option in FilterOptions.Where(opt => opt.IsSelected))
            {
                SelectedOptions.Add(option.Name);
            }

            DebugSelectedOptions();
        }

        // **Filter the displayed options based on user input**
        private void UpdateFilteredOptions()
        {
            var filtered = FilterOptions
                .Where(option => string.IsNullOrWhiteSpace(FilterText) ||
                                 option.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase))
                .ToList();

            FilteredOptions = new ObservableCollection<FilterOption>(filtered);
        }

        // **Debugging Methods**
        public void DebugSelectedOptions()
        {
            Debug.WriteLine($"===== DEBUG: Selected Items for {CriteriaKey} =====");
            Debug.WriteLine($"Selected: {string.Join(", ", SelectedOptions)}");
            Debug.WriteLine($"=============================================");
        }

        public void DebugFilterItem()
        {
            Debug.WriteLine($"===== DEBUG: Filter Item ({CriteriaKey}) =====");
            Debug.WriteLine($"Default Text: {DefaultText}");
            Debug.WriteLine($"Filter Text: {FilterText}");
            Debug.WriteLine($"Available Options: {string.Join(", ", FilterOptions.Select(opt => opt.Name))}");
            Debug.WriteLine($"Number of options: {FilterOptions.Count}");
            Debug.WriteLine($"====================================");
        }
    }

    // **Helper Class for Selection Tracking**
    public class FilterOption : INotifyPropertyChanged
    {
        public string Name { get; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public FilterOption(string name, bool isSelected = false)
        {
            Name = name;
            _isSelected = isSelected;
        }
    }
}
