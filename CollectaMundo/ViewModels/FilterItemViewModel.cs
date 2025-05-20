using CollectaMundo.DomainLogic.Models;
using CollectaMundo.Utilities;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using static CollectaMundo.MainWindow;
using Timer = System.Timers.Timer;

namespace CollectaMundo.ViewModels
{
    /// <summary>
    /// Represents a filterable item in the UI, supporting multi-selection and filtering.
    /// </summary>
    public class FilterItemViewModel : INotifyPropertyChanged
    {
        // Core properties
        public string CriteriaKey { get; }
        public FilterType FilterCategory { get; }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private string? _readableLabel;
        public string? ReadableLabel
        {
            get => _readableLabel;
            set
            {
                if (_readableLabel != value)
                {
                    _readableLabel = value;
                    OnPropertyChanged(nameof(ReadableLabel));
                }
            }
        }

        // Commands
        public ICommand? EmbeddedTextBoxGotFocusCommand { get; }
        public ICommand? EmbeddedTextBoxLostFocusCommand { get; }
        public ICommand? RulesTextBoxGotFocusCommand { get; }
        public ICommand? RulesTextBoxLostFocusCommand { get; }


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
                    _filterViewModel.NotifyFilterChanged();
                }
            }
        }

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

        private Brush _textForeground = Brushes.Gray; // default to gray
        public Brush TextForeground
        {
            get => _textForeground;
            set
            {
                if (_textForeground != value)
                {
                    _textForeground = value;
                    OnPropertyChanged(nameof(TextForeground));
                }
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
                        SelectedSingleOption = FreetextSearch;
                });
            }
            else
            {
                // fallback: just apply the selection directly
                if (!string.IsNullOrWhiteSpace(FreetextSearch) && FreetextSearch != DefaultText)
                    SelectedSingleOption = FreetextSearch;
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

                    _filterViewModel.NotifyFilterChanged();
                }
            }
        }
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

            // Create commands using the helper methods.
            EmbeddedTextBoxGotFocusCommand = CreateGotFocusCommand(() => FilterText = "");
            EmbeddedTextBoxLostFocusCommand = CreateLostFocusCommand(() => FilterText, value => FilterText = value);

            RulesTextBoxGotFocusCommand = CreateGotFocusCommand(() => FreetextSearch = "");
            RulesTextBoxLostFocusCommand = CreateLostFocusCommand(() => FreetextSearch, value => FreetextSearch = value);

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

        // Helper methods for GotFocus/Lostfocus commands.
        private RelayCommand<object> CreateGotFocusCommand(Action clearTextAction)
        {
            return new RelayCommand<object>(_ =>
            {
                clearTextAction();
                TextForeground = Brushes.Black;
                IsDropDownOpen = true;
            });
        }
        private RelayCommand<object> CreateLostFocusCommand(Func<string> getText, Action<string> setText)
        {
            return new RelayCommand<object>(_ =>
            {
                if (string.IsNullOrWhiteSpace(getText()))
                {
                    _suppressFiltering = true;
                    setText(DefaultText);
                    _suppressFiltering = false;
                    TextForeground = Brushes.Gray;
                }
            });
        }


        // Determines whether the given card satisfies this filter.
        // If no value is selected for this filter, returns true.
        //public bool Matches(CardSet card)
        //{
        //    try
        //    {
        //        // Look up the mapping for this filter.
        //        if (!FilterCriteriaMappings.CriteriaMappings.TryGetValue(CriteriaKey, out var mapping))
        //        {
        //            return true; // No mapping? Then don't filter on this criterion.
        //        }

        //        // Special case for color filtering
        //        if (CriteriaKey.Equals("Colors", StringComparison.OrdinalIgnoreCase))
        //        {
        //            // Build sets for mana cost and colors.
        //            var manaCostSymbols = new HashSet<string>(
        //                card.ManaCost != null ? card.ManaCost.Split(',').Select(s => s.Trim()) : [],
        //                StringComparer.OrdinalIgnoreCase);
        //            var colorSymbols = new HashSet<string>(
        //                card.Colors != null ? card.Colors.Split(',').Select(s => s.Trim()) : [],
        //                StringComparer.OrdinalIgnoreCase);

        //            // "Colorless" means no colors are specified.
        //            bool isColorless = string.IsNullOrWhiteSpace(card.Colors);

        //            // For multi-select color filtering, use the selected options.
        //            if (SelectedOptions == null || !SelectedOptions.Any())
        //            {
        //                return true;
        //            }

        //            switch (OperatorSelection)
        //            {
        //                case OperatorType.AND:
        //                    // Every selected color must be present (if "Colorless" is selected, card must be colorless).
        //                    return SelectedOptions.All(opt =>
        //                        opt.Equals("Colorless", StringComparison.OrdinalIgnoreCase) && isColorless ||
        //                        manaCostSymbols.Contains(opt) ||
        //                        colorSymbols.Contains(opt)
        //                    );
        //                case OperatorType.NOT:
        //                    // No selected color should be present.
        //                    return !SelectedOptions.Any(opt =>
        //                        opt.Equals("Colorless", StringComparison.OrdinalIgnoreCase) && isColorless ||
        //                        manaCostSymbols.Contains(opt) ||
        //                        colorSymbols.Contains(opt)
        //                    );
        //                default: // OR case (or any other operator)
        //                    return SelectedOptions.Any(opt =>
        //                        opt.Equals("Colorless", StringComparison.OrdinalIgnoreCase) && isColorless ||
        //                        manaCostSymbols.Contains(opt) ||
        //                        colorSymbols.Contains(opt)
        //                    );
        //            }
        //        }

        //        // Special case for SelectedFinish: perform an exact match.
        //        if (CriteriaKey.Equals("SelectedFinish", StringComparison.OrdinalIgnoreCase))
        //        {
        //            if (SelectedOptions == null || !SelectedOptions.Any())
        //            {
        //                return true;
        //            }

        //            // Use the card's finish value. Adjust this if your card uses a different property.
        //            string cardFinish = card.SelectedFinish ?? string.Empty;
        //            switch (OperatorSelection)
        //            {
        //                case OperatorType.OR:
        //                    // Exact match required.
        //                    return SelectedOptions.Any(opt =>
        //                        string.Equals(opt, cardFinish, StringComparison.OrdinalIgnoreCase));
        //                case OperatorType.NOT:
        //                    return !SelectedOptions.Any(opt =>
        //                        string.Equals(opt, cardFinish, StringComparison.OrdinalIgnoreCase));
        //                default:
        //                    return true;
        //            }
        //        }

        //        // For other filter types, use your existing logic.
        //        // First, try to get the property using the mapping's Property value.
        //        string propertyName = CriteriaKey;
        //        // Optionally also try this.CriteriaKey if necessary:
        //        PropertyInfo? property = typeof(CardSet).GetProperty(propertyName)
        //                              ?? typeof(CardSet).GetProperty(CriteriaKey);

        //        if (property == null)
        //        {
        //            return true;
        //        }

        //        object? value = property.GetValue(card);
        //        string cardValue = value?.ToString() ?? "";

        //        switch (FilterCategory)
        //        {
        //            case FilterType.Single:
        //                if (string.IsNullOrWhiteSpace(SelectedSingleOption) || SelectedSingleOption == DefaultText)
        //                {
        //                    return true;
        //                }

        //                return cardValue.Contains(SelectedSingleOption, StringComparison.OrdinalIgnoreCase);

        //            case FilterType.Multi:
        //                if (SelectedOptions == null || !SelectedOptions.Any())
        //                {
        //                    return true;
        //                }

        //                if (OperatorSelection == OperatorType.AND)
        //                {
        //                    return SelectedOptions.All(opt => cardValue.IndexOf(opt, StringComparison.OrdinalIgnoreCase) >= 0);
        //                }
        //                else if (OperatorSelection == OperatorType.NOT)
        //                {
        //                    return !SelectedOptions.Any(opt => cardValue.IndexOf(opt, StringComparison.OrdinalIgnoreCase) >= 0);
        //                }
        //                else // default OR
        //                {
        //                    return SelectedOptions.Any(opt => cardValue.IndexOf(opt, StringComparison.OrdinalIgnoreCase) >= 0);
        //                }

        //            case FilterType.Numeric:
        //                if (SelectedNumericValue == null)
        //                {
        //                    return true;
        //                }

        //                if (double.TryParse(cardValue, out double cardNumeric))
        //                {
        //                    switch (OperatorSelection)
        //                    {
        //                        case OperatorType.LESS_THAN:
        //                            return cardNumeric < SelectedNumericValue;
        //                        case OperatorType.LESS_THAN_OR_EQUALS:
        //                            return cardNumeric <= SelectedNumericValue;
        //                        case OperatorType.GREATER_THAN:
        //                            return cardNumeric > SelectedNumericValue;
        //                        case OperatorType.GREATER_THAN_OR_EQUALS:
        //                            return cardNumeric >= SelectedNumericValue;
        //                        case OperatorType.EQUALS:
        //                            return Math.Abs(cardNumeric - (double)SelectedNumericValue) < 0.0001;
        //                        case OperatorType.NOT_EQUALS:
        //                            return Math.Abs(cardNumeric - (double)SelectedNumericValue) >= 0.0001;
        //                        default:
        //                            return true;
        //                    }
        //                }
        //                return true;

        //            default:
        //                return true;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.WriteLine($"Error getting matches: {ex.Message}");
        //        MessageBox.Show($"Error getting matches: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        //        return false;
        //    }
        //}
    }
}
