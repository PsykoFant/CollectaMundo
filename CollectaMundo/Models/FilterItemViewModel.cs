using CollectaMundo.Utilities;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using static CollectaMundo.MainWindow;
using Timer = System.Timers.Timer;

namespace CollectaMundo.Models
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

                    _filterViewModel.ApplyFiltering();
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
                    _filterViewModel.ApplyFiltering();
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

            _filterViewModel.ApplyFiltering();
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

            _filterViewModel.ApplyFiltering();
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
        private void TypingTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            // Ensure we run on the UI thread.
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (!string.IsNullOrWhiteSpace(FreetextSearch) && FreetextSearch != DefaultText)
                {
                    // Set the SelectedSingleOption based on the freetext search.
                    // This setter will in turn trigger filtering.
                    SelectedSingleOption = FreetextSearch;
                }
            });
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

                    _filterViewModel.ApplyFiltering();
                }
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

            // Handle Numeric Filters
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
        private RelayCommand CreateGotFocusCommand(Action clearTextAction)
        {
            return new RelayCommand(() =>
            {
                clearTextAction();
                TextForeground = Brushes.Black;
                IsDropDownOpen = true;
            });
        }
        private RelayCommand CreateLostFocusCommand(Func<string> getText, Action<string> setText)
        {
            return new RelayCommand(() =>
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
        public bool Matches(CardSet card)
        {
            try
            {


                // Look up the mapping for this filter.
                if (!Utilities.FilterCriteriaMappings.CriteriaMappings.TryGetValue(this.CriteriaKey, out var mapping))
                {
                    return true; // No mapping? Then don't filter on this criterion.
                }

                // Special case for color filtering
                if (this.CriteriaKey.Equals("Colors", StringComparison.OrdinalIgnoreCase))
                {
                    // Build sets for mana cost and colors.
                    var manaCostSymbols = new HashSet<string>(
                        (card.ManaCost != null ? card.ManaCost.Split(',').Select(s => s.Trim()) : []),
                        StringComparer.OrdinalIgnoreCase);
                    var colorSymbols = new HashSet<string>(
                        (card.Colors != null ? card.Colors.Split(',').Select(s => s.Trim()) : []),
                        StringComparer.OrdinalIgnoreCase);

                    // "Colorless" means no colors are specified.
                    bool isColorless = string.IsNullOrWhiteSpace(card.Colors);

                    // For multi-select color filtering, use the selected options.
                    if (this.SelectedOptions == null || !this.SelectedOptions.Any())
                    {
                        return true;
                    }

                    switch (this.OperatorSelection)
                    {
                        case MainWindow.OperatorType.AND:
                            // Every selected color must be present (if "Colorless" is selected, card must be colorless).
                            return this.SelectedOptions.All(opt =>
                                (opt.Equals("Colorless", StringComparison.OrdinalIgnoreCase) && isColorless) ||
                                manaCostSymbols.Contains(opt) ||
                                colorSymbols.Contains(opt)
                            );
                        case MainWindow.OperatorType.NOT:
                            // No selected color should be present.
                            return !this.SelectedOptions.Any(opt =>
                                (opt.Equals("Colorless", StringComparison.OrdinalIgnoreCase) && isColorless) ||
                                manaCostSymbols.Contains(opt) ||
                                colorSymbols.Contains(opt)
                            );
                        default: // OR case (or any other operator)
                            return this.SelectedOptions.Any(opt =>
                                (opt.Equals("Colorless", StringComparison.OrdinalIgnoreCase) && isColorless) ||
                                manaCostSymbols.Contains(opt) ||
                                colorSymbols.Contains(opt)
                            );
                    }
                }

                // For other filter types, use your existing logic.
                // First, try to get the property using the mapping's Property value.
                string propertyName = mapping.Property;
                // Optionally also try this.CriteriaKey if necessary:
                PropertyInfo? property = typeof(CardSet).GetProperty(propertyName)
                                      ?? typeof(CardSet).GetProperty(this.CriteriaKey);

                if (property == null)
                {
                    return true;
                }

                object? value = property.GetValue(card);
                string cardValue = value?.ToString() ?? "";

                switch (this.FilterCategory)
                {
                    case Utilities.FilterType.Single:
                        if (string.IsNullOrWhiteSpace(this.SelectedSingleOption) || this.SelectedSingleOption == this.DefaultText)
                        {
                            return true;
                        }

                        return cardValue.Contains(this.SelectedSingleOption, StringComparison.OrdinalIgnoreCase);

                    case Utilities.FilterType.Multi:
                        if (this.SelectedOptions == null || !this.SelectedOptions.Any())
                        {
                            return true;
                        }

                        if (this.OperatorSelection == MainWindow.OperatorType.AND)
                        {
                            return this.SelectedOptions.All(opt => cardValue.IndexOf(opt, StringComparison.OrdinalIgnoreCase) >= 0);
                        }
                        else if (this.OperatorSelection == MainWindow.OperatorType.NOT)
                        {
                            return !this.SelectedOptions.Any(opt => cardValue.IndexOf(opt, StringComparison.OrdinalIgnoreCase) >= 0);
                        }
                        else // default OR
                        {
                            return this.SelectedOptions.Any(opt => cardValue.IndexOf(opt, StringComparison.OrdinalIgnoreCase) >= 0);
                        }

                    case Utilities.FilterType.Numeric:
                        if (this.SelectedNumericValue == null)
                        {
                            return true;
                        }

                        if (double.TryParse(cardValue, out double cardNumeric))
                        {
                            switch (this.OperatorSelection)
                            {
                                case MainWindow.OperatorType.LESS_THAN:
                                    return cardNumeric < this.SelectedNumericValue;
                                case MainWindow.OperatorType.LESS_THAN_OR_EQUALS:
                                    return cardNumeric <= this.SelectedNumericValue;
                                case MainWindow.OperatorType.GREATER_THAN:
                                    return cardNumeric > this.SelectedNumericValue;
                                case MainWindow.OperatorType.GREATER_THAN_OR_EQUALS:
                                    return cardNumeric >= this.SelectedNumericValue;
                                case MainWindow.OperatorType.EQUALS:
                                    return Math.Abs(cardNumeric - (double)this.SelectedNumericValue) < 0.0001;
                                case MainWindow.OperatorType.NOT_EQUALS:
                                    return Math.Abs(cardNumeric - (double)this.SelectedNumericValue) >= 0.0001;
                                default:
                                    return true;
                            }
                        }
                        return true;

                    default:
                        return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting matches: {ex.Message}");
                MessageBox.Show($"Error getting matches: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

    }

    // Represents an individual selectable filter option.
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
