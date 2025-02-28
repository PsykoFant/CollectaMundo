using CollectaMundo.Utilities;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;
using static CollectaMundo.MainWindow;
using Timer = System.Timers.Timer;

namespace CollectaMundo.Models
{
    /// <summary>
    /// Represents a filterable item in the UI, supporting multi-selection and filtering.
    /// </summary>
    public class FilterItemViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        // Core properties
        public string CriteriaKey { get; }
        public FilterType FilterCategory { get; }

        // Selection-related properties for single-criteria
        private string? _selectedSingleOption;
        public string? SelectedSingleOption
        {
            get => _selectedSingleOption;
            set
            {
                if (_selectedSingleOption != value)
                {
                    _selectedSingleOption = value;
                    OnPropertyChanged(nameof(SelectedSingleOption));

                    // Update the text box when the value changes externally
                    if (FilterCategory == FilterType.Single)
                    {
                        FreetextSearch = value ?? DefaultText;
                    }

                    // Trigger filtering, but ONLY when the final value is set
                    if (!MainWindow.CurrentInstance._isStartup)
                    {
                        MainWindow.CurrentInstance.FilterVM.DebugFullFilterState();
                    }
                }
            }
        }

        // Selection-related properties for mumeric-criteria
        public ObservableCollection<int>? AvailableNumericOptions { get; }

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
                    MainWindow.CurrentInstance.FilterVM.DebugFullFilterState();
                }
            }
        }

        // Selection-related properties for multi-criteria
        public ObservableCollection<FilterOption> FilterOptions { get; }
        public ObservableCollection<string> SelectedOptions { get; } = [];


        // View model for UI for custom comboboxes
        public bool _suppressFiltering = false; // Used to temporarily disable filtering.
        public ObservableCollection<string> AvailableOptions => [.. FilterOptions.Select(opt => opt.OptionName)];

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

        private void ApplyTextFilter()
        {
            var filtered = FilterOptions.Where(option => string.IsNullOrWhiteSpace(FilterText) || option.OptionName.Contains(FilterText, StringComparison.OrdinalIgnoreCase)).ToList();

            FilteredOptions = [.. filtered];
        }

        private string _freetextSearch = string.Empty;
        public string FreetextSearch
        {
            get => _freetextSearch;
            set
            {
                if (_freetextSearch != value)
                {
                    _freetextSearch = value;
                    OnPropertyChanged(nameof(FreetextSearch));

                    if (FilterCategory == FilterType.Single)
                    {
                        ResetTypingDelay();
                    }
                }
            }
        }

        // Resets the typing delay timer for freetext filtering.

        private readonly Timer? _typingTimer;
        private void ResetTypingDelay()
        {
            _typingTimer?.Stop();
            _typingTimer?.Start();
        }
        // Handles keypress events for the TextBox.
        public void HandleKeyPress(Key key)
        {
            if (FilterCategory != FilterType.Single) return;

            if (key == Key.Enter)
            {
                _typingTimer?.Stop(); // ✅ Cancel delay, apply filtering immediately
                SelectedSingleOption = string.IsNullOrWhiteSpace(FreetextSearch) || FreetextSearch == DefaultText
                    ? null
                    : FreetextSearch;

                MainWindow.CurrentInstance.FilterVM.DebugFullFilterState();
            }
            else if (key == Key.Escape)
            {
                // Reset search box when Escape is pressed
                FreetextSearch = DefaultText;
                SelectedSingleOption = null;
            }
        }

        // Operator selection
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

        // Bindable properties for the two checkboxes
        private bool _isTradeChecked;
        public bool IsTradeChecked
        {
            get => _isTradeChecked;
            set
            {
                if (_isTradeChecked != value)
                {
                    _isTradeChecked = value;
                    OnPropertyChanged(nameof(IsTradeChecked));

                    if (value) // If checked, ensure other checkbox is unchecked
                        IsNotTradeChecked = false;

                    ApplyTradeFilter();
                }
            }
        }

        private bool _isNotTradeChecked;
        public bool IsNotTradeChecked
        {
            get => _isNotTradeChecked;
            set
            {
                if (_isNotTradeChecked != value)
                {
                    _isNotTradeChecked = value;
                    OnPropertyChanged(nameof(IsNotTradeChecked));

                    if (value) // If checked, ensure other checkbox is unchecked
                        IsTradeChecked = false;

                    ApplyTradeFilter();
                }
            }
        }

        // This applies the filtering logic whenever a checkbox is clicked
        private void ApplyTradeFilter()
        {
            if (IsTradeChecked)
            {
                SelectedNumericValue = 0;
                OperatorSelection = OperatorType.GREATER_THAN; // CardsForTrade > 0
            }
            else if (IsNotTradeChecked)
            {
                SelectedNumericValue = 0;
                OperatorSelection = OperatorType.EQUALS; // CardsForTrade == 0
            }
            else
            {
                SelectedNumericValue = null;
            }

            // Debug output (will later be replaced with actual filtering)
            MainWindow.CurrentInstance.FilterVM.DebugFullFilterState();
        }

        // Constructor - Initializes filter options and selection tracking.
        private readonly FilterViewModel _filterViewModel;
        public FilterItemViewModel(
    string criteriaKey,
    IEnumerable<FilterOption> filterOptions,
    string defaultText,
    FilterViewModel filterViewModel,
    IEnumerable<int>? numericOptions = null)
        {
            _filterViewModel = filterViewModel ?? throw new ArgumentNullException(nameof(filterViewModel));
            CriteriaKey = criteriaKey;
            DefaultText = defaultText;
            _filterText = DefaultText;
            _freetextSearch = DefaultText;

            // Initialize FilterOptions (using a concrete collection type)
            FilterOptions = new ObservableCollection<FilterOption>(filterOptions);

            // Initially, show all options
            _filteredOptions = new ObservableCollection<FilterOption>(FilterOptions);

            // Handle Numeric Filters
            if (numericOptions != null)
            {
                AvailableNumericOptions = new ObservableCollection<int>(numericOptions);
            }

            // Subscribe to selection changes in checkboxes (for Multi-selection filters)
            foreach (var filterOption in FilterOptions)
            {
                filterOption.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(FilterOption.IsSelected))
                        UpdateSelectedOptions();
                };
            }

            // Retrieve filter configuration from FilterCriteriaMappings
            if (FilterCriteriaMappings.CriteriaMappings.TryGetValue(criteriaKey, out var mapping))
            {
                FilterCategory = mapping.Type;
                AvailableOperators = mapping.Operators != null ? new ObservableCollection<OperatorType>(mapping.Operators) : null;
                OperatorSelection = mapping.Operators?.FirstOrDefault() ?? OperatorType.OR;

                // Initialize typing delay timer for Single (freetext) filters
                if (FilterCategory == FilterType.Single)
                {
                    _typingTimer = new Timer(1500) { AutoReset = false };
                    _typingTimer.Elapsed += (_, _) =>
                    {
                        if (!string.IsNullOrWhiteSpace(FreetextSearch) && FreetextSearch != DefaultText)
                        {
                            SelectedSingleOption = FreetextSearch; // Assign value before filtering
                        }
                    };
                }
            }
        }

        // Updates the selected options when checkboxes are toggled.
        private void UpdateSelectedOptions()
        {
            SelectedOptions.Clear();
            foreach (var option in FilterOptions.Where(opt => opt.IsSelected))
                SelectedOptions.Add(option.OptionName);

            _filterViewModel.DebugFullFilterState();
        }

        // Debugging Methods
        public void DebugFilterItem()
        {
            Debug.WriteLine($"===== DEBUG: Filter Item ({CriteriaKey}) =====");
            Debug.WriteLine($"Default Text: {DefaultText}");
            Debug.WriteLine($"Filter Text: {FilterText}");
            Debug.WriteLine($"Available Options: {string.Join(", ", FilterOptions.Select(opt => opt.OptionName))}");
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
