using CollectaMundo.Utilities;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using static CollectaMundo.MainWindow;

namespace CollectaMundo.Models
{
    /// <summary>
    /// Represents a filterable item in the UI, supporting multi-selection and filtering.
    /// </summary>
    public class FilterItemViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        // 🔹 Core properties
        public string CriteriaKey { get; }
        public bool _suppressFiltering = false; // Used to temporarily disable filtering.

        // Selection-related properties for multi-criteria
        public ObservableCollection<FilterOption> FilterOptions { get; }
        public ObservableCollection<string> SelectedOptions { get; } = [];

        // 🔹 Operator selection
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

                    if (!MainWindow.CurrentInstance._isStartup)
                    {
                        MainWindow.CurrentInstance.FilterVM.DebugFullFilterState();
                    }
                }
            }
        }

        // 🔹 Filtered text-based options
        public ObservableCollection<string> AvailableOptions => [.. FilterOptions.Select(opt => opt.OptionName)];

        // 🔹 Filter text (for searching in options)
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
                        ApplyTextFilter();
                }
            }
        }
        public string DefaultText { get; }

        // Filtered options in custom comboboxes (updates based on text search)
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

        // UI dropdown open state
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

        public FilterType FilterCategory { get; }


        /// <summary>
        /// Constructor - Initializes filter options and selection tracking.
        /// </summary>
        public FilterItemViewModel(string criteriaKey, IEnumerable<string> availableOptions, string defaultText)
        {
            CriteriaKey = criteriaKey;
            DefaultText = defaultText;
            _filterText = DefaultText;

            // Convert available options into FilterOption objects
            FilterOptions = [.. availableOptions.Select(option => new FilterOption(option))];

            // Initially, show all options
            _filteredOptions = [.. FilterOptions];

            // Subscribe to selection changes in checkboxes
            foreach (var filterOption in FilterOptions)
            {
                filterOption.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(FilterOption.IsSelected))
                        UpdateSelectedOptions();
                };
            }

            if (FilterCriteriaMappings.CriteriaMappings.TryGetValue(criteriaKey, out var mapping))
            {
                FilterCategory = mapping.Type;
                AvailableOperators = mapping.Operators != null ? [.. mapping.Operators] : null;
                OperatorSelection = mapping.Operators?.FirstOrDefault() ?? OperatorType.OR;
            }
        }

        /// <summary>
        /// Updates the selected options when checkboxes are toggled.
        /// </summary>
        private void UpdateSelectedOptions()
        {
            SelectedOptions.Clear();
            foreach (var option in FilterOptions.Where(opt => opt.IsSelected))
                SelectedOptions.Add(option.OptionName);

            MainWindow.CurrentInstance.FilterVM.DebugFullFilterState();
        }

        /// <summary>
        /// Applies text-based filtering to the options.
        /// </summary>
        private void ApplyTextFilter()
        {
            var filtered = FilterOptions.Where(option => string.IsNullOrWhiteSpace(FilterText) || option.OptionName.Contains(FilterText, StringComparison.OrdinalIgnoreCase)).ToList();

            FilteredOptions = [.. filtered];
        }

        // 🔹 Debugging Methods

        /// <summary>
        /// Logs filter state for debugging.
        /// </summary>
        public void DebugFilterItem()
        {
            Debug.WriteLine($"===== DEBUG: Filter Item ({CriteriaKey}) =====");
            Debug.WriteLine($"Default Text: {DefaultText}");
            Debug.WriteLine($"Filter Text: {FilterText}");
            //Debug.WriteLine($"Available Options: {string.Join(", ", FilterOptions.Select(opt => opt.OptionName))}");
            Debug.WriteLine($"Number of options: {FilterOptions.Count}");
            Debug.WriteLine($"====================================");
        }
    }

    /// <summary>
    /// Represents an individual selectable filter option.
    /// </summary>
    public class FilterOption(string optionName, bool isSelected = false) : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public string OptionName { get; } = optionName;

        private bool _isSelected = isSelected;
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
    }
}
