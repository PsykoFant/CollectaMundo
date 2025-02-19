using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;

namespace CollectaMundo.Models
{
    public class FilterItemViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        public string CriteriaKey { get; }

        public bool _suppressFiltering = false; // Used to temporarily disable filtering
        public ObservableCollection<string> AvailableOptions { get; }

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

                    if (!_suppressFiltering) // Only update filtering when allowed
                    {
                        UpdateFilteredOptions();
                    }
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
        public FilterItemViewModel(string criteriaKey, ObservableCollection<string> availableOptions, string defaultText)
        {
            CriteriaKey = criteriaKey;
            AvailableOptions = availableOptions;
            DefaultText = defaultText;

            // Set FilterText to DefaultText at startup
            _filterText = DefaultText;

            _filteredOptions = [.. availableOptions];
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
        private void UpdateFilteredOptions()
        {
            var filtered = AvailableOptions
                .Where(option => string.IsNullOrWhiteSpace(FilterText) ||
                                 option.IndexOf(FilterText, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            FilteredOptions = [.. filtered];
        }

        public void DebugFilterItem()
        {
            Debug.WriteLine($"===== DEBUG: Filter Item ({CriteriaKey}) =====");
            Debug.WriteLine($"Default Text: {DefaultText}");
            Debug.WriteLine($"Filter Text: {FilterText}");
            Debug.WriteLine($"Available Options: {string.Join(", ", AvailableOptions)}");
            Debug.WriteLine($"====================================");
        }
    }

}
