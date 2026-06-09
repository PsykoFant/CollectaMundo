using CollectaMundo.DomainLogic.Filtering;
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
using System.Windows.Threading;
using Timer = System.Timers.Timer;

namespace CollectaMundo.ViewModels.Filtering
{
    public partial class FilterItemViewModel : ObservableObject
    {
        // Core properties
        public string CriteriaKey { get; }
        public FilterType FilterCategory { get; }
        public string DefaultText { get; }

        [ObservableProperty]
        private string? readableLabel;

        [ObservableProperty]
        private Brush textForeground = Brushes.Gray;

        [ObservableProperty]
        private bool isDropDownOpen;

        [ObservableProperty]
        private int? selectedNumericValue;

        [ObservableProperty]
        private bool clearComboBoxSelectionTrigger;
        partial void OnSelectedNumericValueChanged(int? value) => _filterViewModel.NotifyFilterChanged();

        [ObservableProperty]
        private OperatorType operatorSelection;
        partial void OnOperatorSelectionChanged(OperatorType value) => _filterViewModel.NotifyFilterChanged();

        [ObservableProperty]
        private string? selectedSingleOption;
        partial void OnSelectedSingleOptionChanged(string? value)
        {
            _filterViewModel.NotifyFilterChanged();
        }

        [ObservableProperty]
        private string freetextSearch = string.Empty;
        partial void OnFreetextSearchChanged(string value)
        {
            FilterText = value;

            if (string.IsNullOrWhiteSpace(value))
            {
                ApplyTextFilter();

                if (SelectedSingleOption != null)
                {
                    _ignoreNextSelectionChanged = true; // prevent next SelectionChanged handler
                    SelectedSingleOption = null;
                    ClearComboBoxSelectionTrigger = true;
                    ClearComboBoxSelectionTrigger = false;
                }
            }
            else
            {
                if (!_isSelectionInProgress)
                {
                    if (OperatorSelection == OperatorType.EQUALS)
                    {
                        OperatorSelection = OperatorType.CONTAINS;
                    }
                    ResetTypingDelay();
                }
            }
        }

        [ObservableProperty]
        private string filterText;
        partial void OnFilterTextChanged(string value)
        {
            if (_initialized && !_suppressFiltering)
            {
                ApplyTextFilter();
            }
        }

        [ObservableProperty]
        private bool isTradeChecked;
        partial void OnIsTradeCheckedChanged(bool value)
        {
            if (value)
            {
                IsNotTradeChecked = false;
            }

            ApplyTradeFilter();
        }

        [ObservableProperty]
        private bool isNotTradeChecked;
        partial void OnIsNotTradeCheckedChanged(bool value)
        {
            if (value)
            {
                IsTradeChecked = false;
            }

            ApplyTradeFilter();
        }
        public ObservableCollection<FilterOption> FilterOptions { get; }
        public ObservableCollection<FilterOption> FilteredOptions { get; private set; }
        public ObservableCollection<string> SelectedOptions { get; } = [];
        public ObservableCollection<string> AvailableOptions => [.. FilterOptions.Select(opt => opt.DisplayName)];
        public IReadOnlyList<string> SelectedOptionDisplayNames => [.. FilterOptions.Where(o => o.IsSelected).Select(o => o.DisplayName)];
        public ObservableCollection<OperatorType>? AvailableOperators { get; }
        public ObservableCollection<int>? AvailableNumericOptions { get; }

        // Resets filter options, preserving selection where possible
        public void ResetOptions(IEnumerable<string> newOptionNames)
        {
            var incoming = _filterItemSearchLogic.NormalizeOptionNames(newOptionNames);

            ResetOptions(incoming.Select(name => new FilterOption(name, name)));
        }
        public void ResetOptions(IEnumerable<FilterOption> newOptions)
        {
            var incoming = _filterItemSearchLogic.BuildOptions(newOptions);

            if (_filterItemSearchLogic.IsEquivalentOptionList(FilterOptions, incoming))
            {
                return;
            }

            var selectedValues = FilterOptions
                .Where(o => o.IsSelected)
                .Select(o => o.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var opt in FilterOptions)
            {
                opt.PropertyChanged -= FilterOption_PropertyChanged;
            }

            FilterOptions.Clear();

            foreach (var opt in incoming)
            {
                opt.IsSelected = selectedValues.Contains(opt.Value);
                opt.PropertyChanged += FilterOption_PropertyChanged;
                FilterOptions.Add(opt);
            }

            ApplyTextFilter();
            UpdateSelectedOptions();
            OnPropertyChanged(nameof(AvailableOptions));
        }

        private readonly FilterViewModel _filterViewModel;
        private readonly IFilterItemSearchLogic _filterItemSearchLogic;
        private readonly Timer? _typingTimer;
        private bool _isSelectionInProgress = false;
        private bool _ignoreNextSelectionChanged;
        private readonly bool _initialized = false;
        private bool _suppressFiltering = false; // Used to temporarily suppress filter update

        // Constructor
        public FilterItemViewModel(string criteriaKey, IEnumerable<FilterOption> filterOptions, string defaultText, string readableLabel, FilterViewModel filterViewModel, IFilterItemSearchLogic filterItemSearchLogic, IEnumerable<int>? numericOptions = null)
        {
            _filterViewModel = filterViewModel;
            _filterItemSearchLogic = filterItemSearchLogic;

            CriteriaKey = criteriaKey;
            DefaultText = defaultText;
            ReadableLabel = readableLabel;

            FilterText = DefaultText;
            FreetextSearch = defaultText;

            FilterOptions = [.. filterOptions];
            FilteredOptions = [.. FilterOptions];

            if (numericOptions != null)
            {
                AvailableNumericOptions = [.. numericOptions];
            }

            foreach (var filterOption in FilterOptions)
            {
                filterOption.PropertyChanged += FilterOption_PropertyChanged;
            }

            if (FilterCriteriaMappings.CriteriaMappings.TryGetValue(criteriaKey, out var mapping))
            {
                FilterCategory = mapping.Type;
                AvailableOperators = mapping.Operators != null ? [.. mapping.Operators] : null;
                OperatorSelection = mapping.Operators?.FirstOrDefault() ?? OperatorType.OR;

                if (FilterCategory == FilterType.Single)
                {
                    _typingTimer = new Timer(200) { AutoReset = false };
                    _typingTimer.Elapsed += TypingTimer_Elapsed;
                }
            }
            _initialized = true; // allow ApplyTextFilter to trigger from now on
        }

        // Update SelectedOptions when checkboxes change
        private void FilterOption_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FilterOption.IsSelected))
            {
                UpdateSelectedOptions();
            }
        }
        private void UpdateSelectedOptions()
        {
            SelectedOptions.Clear();

            foreach (var opt in _filterItemSearchLogic.ExtractSelectedOptions(FilterOptions))
            {
                SelectedOptions.Add(opt);
            }

            _filterViewModel.NotifyFilterChanged();
        }
        private void ApplyTextFilter()
        {
            var effectiveFilterText =
                string.Equals(FilterText, DefaultText, StringComparison.Ordinal)
                    ? string.Empty
                    : FilterText;

            FilteredOptions = new ObservableCollection<FilterOption>(_filterItemSearchLogic.ApplyTextFilter(FilterOptions, effectiveFilterText));

            OnPropertyChanged(nameof(FilteredOptions));
        }
        private void ApplyTradeFilter()
        {
            if (IsTradeChecked)
            {
                SelectedNumericValue = 0;
                OperatorSelection = OperatorType.GREATER_THAN;
            }
            else if (IsNotTradeChecked)
            {
                SelectedNumericValue = 0;
                OperatorSelection = OperatorType.EQUALS;
            }
            else
            {
                SelectedNumericValue = null;
            }

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
                disp.Invoke(ApplyTypingSelection);
            }
            else
            {
                ApplyTypingSelection();
            }
        }
        protected virtual void ApplyTypingSelection()
        {
            // If user already selected, don't override with CONTAINS
            if (OperatorSelection == OperatorType.EQUALS)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(FreetextSearch) && FreetextSearch != DefaultText)
            {
                OperatorSelection = OperatorType.CONTAINS;
                SelectedSingleOption = FreetextSearch;

                Debug.WriteLine($"{DateTime.Now:HH:mm:ss.fff} - TypingTimer_Elapsed → CONTAINS: {FreetextSearch}");
            }
        }

        // Handles special key behavior for single filters
        protected internal void HandleKeyLogic(Key key)
        {
            if (FilterCategory != FilterType.Single)
            {
                return;
            }

            if (key == Key.Enter)
            {
                _typingTimer?.Stop();
                SelectedSingleOption = string.IsNullOrWhiteSpace(FreetextSearch) || FreetextSearch == DefaultText
                    ? null
                    : FreetextSearch;
            }
            else if (key == Key.Escape)
            {
                FreetextSearch = DefaultText;
                SelectedSingleOption = null;
                TextForeground = Brushes.Gray;
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
        public void TextBoxGotFocus(object? _)
        {
            FreetextSearch = "";
            TextForeground = Brushes.Black;
            IsDropDownOpen = true;
        }

        [RelayCommand]
        public void TextBoxLostFocus(object? _)
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
            {
                return;
            }

            var key = e.Key;
            HandleKeyLogic(key);

            if (key == Key.Escape)
            {
                // Run focus clear on dispatcher (as before)
                Application.Current?.Dispatcher?.InvokeAsync(() =>
                {
                    // SAFETY GUARD: ensure MainWindow has a valid visual source (HWND ready)
                    var mainWindow = Application.Current?.MainWindow;
                    if (mainWindow == null || PresentationSource.FromVisual(mainWindow) == null)
                    {
                        Debug.WriteLine("⚠️ Skipped focus clear - MainWindow not ready");
                        return; // Abort early to prevent "Hwnd of zero is not valid"
                    }

                    // Safe focus clearing logic
                    var scope = FocusManager.GetFocusScope(Keyboard.FocusedElement as DependencyObject);
                    FocusManager.SetFocusedElement(scope, null);
                    Keyboard.ClearFocus();

                }, DispatcherPriority.Background);
            }

            e.Handled = key is Key.Enter or Key.Escape;
        }

        [RelayCommand]
        public void ComboBoxSelectionChanged(object? selectedItem)
        {
            if (_ignoreNextSelectionChanged)
            {
                _ignoreNextSelectionChanged = false;
                return;
            }

            if (FilterCategory == FilterType.Single && selectedItem is FilterOption opt)
            {
                _typingTimer?.Stop();

                _isSelectionInProgress = true;

                OperatorSelection = OperatorType.EQUALS;
                SelectedSingleOption = opt.Value;
                FreetextSearch = opt.DisplayName;

                _isSelectionInProgress = false;
            }
        }

        #endregion
    }
}
