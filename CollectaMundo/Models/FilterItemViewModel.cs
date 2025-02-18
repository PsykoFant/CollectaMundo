using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;

namespace CollectaMundo.Models
{
    public class FilterItemViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        public string CriteriaKey { get; } // e.g. "Rarity"
        public ObservableCollection<string> AvailableOptions { get; } // Full list of options

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
                    OnPropertyChanged(nameof(FilteredOptions)); // Notify UI
                }
            }
        }
        public ObservableCollection<string> FilteredOptions
        {
            get
            {
                var filtered = AvailableOptions
                    .Where(option => string.IsNullOrWhiteSpace(FilterText) ||
                                     option.IndexOf(FilterText, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();

                return new ObservableCollection<string>(filtered);
            }
        }

        private bool _isDropDownOpen;
        public bool IsDropDownOpen
        {
            get => _isDropDownOpen;
            set
            {
                if (_isDropDownOpen != value)
                {
                    _isDropDownOpen = value;
                    OnPropertyChanged(nameof(IsDropDownOpen));
                }
            }
        }
        public string DefaultText { get; }
        public FilterItemViewModel(string criteriaKey, ObservableCollection<string> availableOptions, string defaultText)
        {
            CriteriaKey = criteriaKey;
            AvailableOptions = availableOptions;
            DefaultText = defaultText;
        }

        public void DebugFilterItem()
        {
            Debug.WriteLine($"===== DEBUG: Filter Item ({CriteriaKey}) =====");
            Debug.WriteLine($"Default Text: {DefaultText}");
            Debug.WriteLine($"Available Options: {string.Join(", ", AvailableOptions)}");
            Debug.WriteLine($"====================================");
        }
    }
}
