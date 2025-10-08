using CollectaMundo.DomainLogic.Filtering;
using CollectaMundo.DomainLogic.Filtering.Enums;
using CollectaMundo.DomainLogic.Filtering.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Timer = System.Timers.Timer;

namespace CollectaMundo.ViewModels
{
    public partial class FilterItemViewModel : ObservableObject
    {
        // Core identifiers
        public string CriteriaKey { get; }
        public FilterType FilterCategory { get; }

        // Collaborators
        private readonly FilterViewModel _filterViewModel;
        private readonly IFilterItemSearchLogic _filterItemSearchLogic;

        // Constructor
        public FilterItemViewModel(string criteriaKey, IEnumerable<FilterOption> filterOptions, string defaultText, string readableLabel, FilterViewModel filterViewModel, IFilterItemSearchLogic filterItemSearchLogic, IEnumerable<int>? numericOptions = null)
        {
            _filterViewModel = filterViewModel;
            _filterItemSearchLogic = filterItemSearchLogic;
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

        // Core state + selections
        [ObservableProperty] private string? readableLabel;
        [ObservableProperty] private bool isDropDownOpen;
        [ObservableProperty] private Brush textForeground = Brushes.Gray;

        [ObservableProperty] private int? selectedNumericValue;
        partial void OnSelectedNumericValueChanged(int? value)
        {
            _filterViewModel.NotifyFilterChanged();
        }

        [ObservableProperty]
        private OperatorType operatorSelection;
        partial void OnOperatorSelectionChanged(OperatorType value)
        {
            _filterViewModel.NotifyFilterChanged();
        }

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
        public ObservableCollection<int>? AvailableNumericOptions { get; }
        public ObservableCollection<OperatorType>? AvailableOperators { get; }
        public ObservableCollection<FilterOption> FilterOptions { get; }
        public ObservableCollection<string> AvailableOptions => [.. FilterOptions.Select(o => o.OptionName)];
        public ObservableCollection<string> SelectedOptions { get; } = [];

        // Internal filter logic
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
                    FilterText = value;

                    if (string.IsNullOrWhiteSpace(value))
                    {
                        ApplyTextFilter();

                        // Manually reset the selection and trigger filtering
                        if (FilterCategory == FilterType.Single)
                        {
                            SelectedSingleOption = string.Empty;
                        }
                    }
                    else if (FilterCategory == FilterType.Single)
                    {
                        ResetTypingDelay();
                    }
                }
            }
        }


        // Trade checkbox logic (non-MVVM-observable for toggle binding simplicity)
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

                    if (value) IsNotTradeChecked = false;
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

                    if (value) IsTradeChecked = false;
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

        // Logic helpers
        private void ApplyTextFilter()
        {
            FilteredOptions = new ObservableCollection<FilterOption>(_filterItemSearchLogic.ApplyTextFilter(FilterOptions, FilterText));
        }
        private void UpdateSelectedOptions()
        {
            SelectedOptions.Clear();
            foreach (var opt in _filterItemSearchLogic.ExtractSelectedOptions(FilterOptions))
                SelectedOptions.Add(opt);

            _filterViewModel.NotifyFilterChanged();
        }
        private void ResetTypingDelay()
        {
            _typingTimer?.Stop();
            _typingTimer?.Start();
        }
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

        private readonly Timer? _typingTimer;

        private bool _suppressFiltering = false;

        // Used when a dynamic set of options replaces the current list
        public void ResetOptions(IEnumerable<string> newOptionNames)
        {
            // Fast no-op if identical (order-insensitive compare)
            var incoming = _filterItemSearchLogic.NormalizeOptionNames(newOptionNames);
            var current = FilterOptions.Select(o => o.OptionName);

            if (_filterItemSearchLogic.IsEquivalentOptionList(current, incoming))
                return;

            // Unsubscribe old handlers to avoid leaks
            foreach (var opt in FilterOptions)
            {
                opt.PropertyChanged -= FilterOption_PropertyChanged;
            }

            // Replace contents (preserve the same ObservableCollection instance)
            FilterOptions.Clear();
            var newOptions = _filterItemSearchLogic.BuildOptionsFromNames(incoming);

            foreach (var opt in newOptions)
            {
                opt.PropertyChanged += FilterOption_PropertyChanged;
                FilterOptions.Add(opt);
            }

            // Rebuild filtered view and selected state
            ApplyTextFilter();
            UpdateSelectedOptions();

            // Also raise any dependent properties
            OnPropertyChanged(nameof(AvailableOptions));
        }
        private void FilterOption_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FilterOption.IsSelected))
            {
                UpdateSelectedOptions();
            }
        }

        #region RelayCommands

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

        [RelayCommand]
        public void RulesTextBoxGotFocus(object? _)
        {
            FreetextSearch = "";
            TextForeground = Brushes.Black;
            IsDropDownOpen = true;
        }

        [RelayCommand]
        public void RulesTextBoxLostFocus(object? _)
        {
            if (string.IsNullOrWhiteSpace(FreetextSearch))
            {
                _suppressFiltering = true;
                FreetextSearch = DefaultText;
                _suppressFiltering = false;
                TextForeground = Brushes.Gray;
            }
        }

        [RelayCommand]
        private void KeyPressed(KeyEventArgs e)
        {
            if (FilterCategory != FilterType.Single)
                return;

            if (e.Key == Key.Enter)
            {
                _typingTimer?.Stop();
                SelectedSingleOption = string.IsNullOrWhiteSpace(FreetextSearch) || FreetextSearch == DefaultText
                    ? null
                    : FreetextSearch;

                e.Handled = true; // Optional: prevent bubbling
            }
            else if (e.Key == Key.Escape)
            {
                FreetextSearch = DefaultText;
                SelectedSingleOption = null;
                TextForeground = Brushes.Gray; // Reset text color

                // Clear focus after slight delay
                Application.Current?.Dispatcher?.InvokeAsync(() =>
                {
                    var scope = FocusManager.GetFocusScope(Keyboard.FocusedElement as DependencyObject);
                    FocusManager.SetFocusedElement(scope, null);
                    Keyboard.ClearFocus();
                }, DispatcherPriority.Background);

                e.Handled = true;
            }
        }

        #endregion
    }

}
