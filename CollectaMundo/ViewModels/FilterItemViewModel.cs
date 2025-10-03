using CollectaMundo.DomainLogic.Filtering.Enums;
using CollectaMundo.DomainLogic.Filtering.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Timer = System.Timers.Timer;

namespace CollectaMundo.ViewModels
{
    /// <summary>
    /// Represents a filterable item in the UI, supporting multi-selection and filtering.
    /// </summary>
    public partial class FilterItemViewModel : ObservableObject
    {
        // Core properties
        public string CriteriaKey { get; }
        public FilterType FilterCategory { get; }

        [ObservableProperty]
        private string? readableLabel;

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

                    // For single filters, update the associated text field.
                    if (FilterCategory == FilterType.Single)
                    {
                        FreetextSearch = value ?? DefaultText;
                    }

                    _filterViewModel.NotifyFilterChanged();
                }
            }
        }
        public ObservableCollection<string> AvailableOptions => [.. FilterOptions.Select(opt => opt.OptionName)];

        // Selection-related properties for mumeric-criteria
        public ObservableCollection<int>? AvailableNumericOptions { get; }

        [ObservableProperty]
        private int? selectedNumericValue;

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
                    {
                        IsNotTradeChecked = false;
                    }

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
                    {
                        IsTradeChecked = false;
                    }

                    ApplyTradeFilter();
                }
            }
        }
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

            _filterViewModel.NotifyFilterChanged();
        }

        // Selection-related properties for multi-criteria
        public ObservableCollection<FilterOption> FilterOptions { get; }

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
        public ObservableCollection<string> SelectedOptions { get; } = [];

        // Updates the selected options when checkboxes are toggled.
        private void UpdateSelectedOptions()
        {
            SelectedOptions.Clear();
            foreach (var option in FilterOptions.Where(opt => opt.IsSelected))
            {
                SelectedOptions.Add(option.OptionName);
            }

            _filterViewModel.NotifyFilterChanged();
        }

        // Handle UI properties in custom comboboxes (e.g. filtering options in dropdown)
        public bool _suppressFiltering = false; // Used to temporarily disable filtering.

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
                        ApplyTextFilter();
                    }
                }
            }
        }
        public string DefaultText { get; }

        [ObservableProperty]
        private bool isDropDownOpen;

        [ObservableProperty]
        private Brush textForeground = Brushes.Gray;
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

                    // Update FilterText so the combobox dropdown items are filtered immediately.
                    FilterText = value;

                    if (FilterCategory == FilterType.Single)
                    {
                        ResetTypingDelay();
                    }
                }
            }
        }

        // Resets the typing delay timer for rulestext freetext filtering.

        private readonly Timer? _typingTimer;
        private void TypingTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            var disp = Application.Current?.Dispatcher;
            if (disp != null)
            {
                disp.Invoke(() =>
                {
                    if (!string.IsNullOrWhiteSpace(FreetextSearch) && FreetextSearch != DefaultText)
                    {
                        SelectedSingleOption = FreetextSearch;
                    }
                });
            }
            else
            {
                // fallback: just apply the selection directly
                if (!string.IsNullOrWhiteSpace(FreetextSearch) && FreetextSearch != DefaultText)
                {
                    SelectedSingleOption = FreetextSearch;
                }
            }
        }
        private void ResetTypingDelay()
        {
            _typingTimer?.Stop();
            _typingTimer?.Start();
        }
        public void HandleKeyPress(Key key)
        {
            if (FilterCategory != FilterType.Single)
            {
                return;
            }

            if (key == Key.Enter)
            {
                _typingTimer?.Stop(); // Cancel delay, apply filtering immediately
                SelectedSingleOption = string.IsNullOrWhiteSpace(FreetextSearch) || FreetextSearch == DefaultText
                    ? null
                    : FreetextSearch;

                //_filterViewModel.DebugFullFilterState();
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

        [ObservableProperty]
        private OperatorType operatorSelection;
        public bool IsDefault
        {
            get
            {
                // For a single filter, it's default if no selection has been made or it equals the default text.
                if (FilterCategory == FilterType.Single)
                {
                    return string.IsNullOrWhiteSpace(SelectedSingleOption) || SelectedSingleOption == DefaultText;
                }
                // For a multi filter, it's default if no options are selected.
                if (FilterCategory == FilterType.Multi)
                {
                    return SelectedOptions == null || !SelectedOptions.Any();
                }
                // For a numeric filter, it's default if no numeric value is selected.
                if (FilterCategory == FilterType.Numeric)
                {
                    return SelectedNumericValue == null;
                }
                return true;
            }
        }

        // Constructor - Initializes filter options and selection tracking.
        private readonly FilterViewModel _filterViewModel;
        public FilterItemViewModel(string criteriaKey, IEnumerable<FilterOption> filterOptions, string defaultText, string readableLabel, FilterViewModel filterViewModel, IEnumerable<int>? numericOptions = null)
        {
            Debug.WriteLine("FilterItemViewModel created: " + criteriaKey);

            _filterViewModel = filterViewModel ?? throw new ArgumentNullException(nameof(filterViewModel));
            CriteriaKey = criteriaKey;
            DefaultText = defaultText;
            ReadableLabel = readableLabel;
            _filterText = DefaultText;
            _freetextSearch = DefaultText;

            // Initialize FilterOptions (using a concrete collection type)
            FilterOptions = [.. filterOptions];

            // Initially, show all options
            _filteredOptions = [.. FilterOptions];

            // Handle Numeric FilterBase
            if (numericOptions != null)
            {
                AvailableNumericOptions = [.. numericOptions];
            }

            // Subscribe to selection changes in checkboxes (for Multi-selection filters)
            foreach (var filterOption in FilterOptions)
            {
                filterOption.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(FilterOption.IsSelected))
                    {
                        UpdateSelectedOptions();
                    }
                };
            }

            // Retrieve filter configuration from FilterCriteriaMappings
            if (FilterCriteriaMappings.CriteriaMappings.TryGetValue(criteriaKey, out var mapping))
            {
                FilterCategory = mapping.Type;
                AvailableOperators = mapping.Operators != null ? [.. mapping.Operators] : null;
                OperatorSelection = mapping.Operators?.FirstOrDefault() ?? OperatorType.OR;

                // Initialize typing delay timer for Single (freetext) filters
                if (FilterCategory == FilterType.Single)
                {
                    _typingTimer = new Timer(200) { AutoReset = false };
                    _typingTimer.Elapsed += TypingTimer_Elapsed;
                }
            }
        }

        // Commands for handling focus events on the embedded TextBox in the ComboBox
        [RelayCommand]
        private void OnEmbeddedTextBoxGotFocus(object? _)
        {
            FilterText = "";
            TextForeground = Brushes.Black;
            IsDropDownOpen = true;
        }

        [RelayCommand]
        private void OnEmbeddedTextBoxLostFocus(object? _)
        {
            if (string.IsNullOrWhiteSpace(FilterText))
            {
                _suppressFiltering = true;
                FilterText = DefaultText;
                _suppressFiltering = false;
                TextForeground = Brushes.Gray;
            }
        }

        public void ResetOptions(IEnumerable<string> newOptionNames)
        {
            // Fast no-op if identical (order-insensitive compare)
            var incoming = newOptionNames
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var current = FilterOptions.Select(o => o.OptionName).ToList();
            bool same = current.Count == incoming.Count &&
                        current.SequenceEqual(incoming, StringComparer.OrdinalIgnoreCase);
            if (same)
            {
                return;
            }

            // 1) Unsubscribe old handlers to avoid leaks
            foreach (var opt in FilterOptions)
            {
                opt.PropertyChanged -= FilterOption_PropertyChanged;
            }

            // 2) Replace FilterOptions
            FilterOptions.Clear();
            foreach (var name in incoming)
            {
                var opt = new FilterOption(name);
                opt.PropertyChanged += FilterOption_PropertyChanged;
                FilterOptions.Add(opt);
            }

            // 3) Refresh FilteredOptions right now (so ToggleButton path shows latest)
            if (string.IsNullOrWhiteSpace(FilterText) || FilterText == DefaultText)
            {
                // Show all if there is no active text filter
                FilteredOptions = new ObservableCollection<FilterOption>(FilterOptions);
            }
            else
            {
                ApplyTextFilter();
            }

            // 4) AvailableOptions depends on FilterOptions
            OnPropertyChanged(nameof(AvailableOptions));
        }

        // centralize the handler so we can attach/detach
        private void FilterOption_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FilterOption.IsSelected))
            {
                UpdateSelectedOptions();
            }
        }
    }
}
