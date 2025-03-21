using CollectaMundo.ViewModels;
using System.ComponentModel;

namespace CollectaMundo.Models
{
    // Base class for all filters with common properties.
    public abstract class Filters
    {
        public required string CriteriaKey { get; set; }
    }
    // Default values and options for a filter.
    public class FilterDefaults : Filters, INotifyPropertyChanged
    {
        public List<FilterOption> FilterOptions { get; set; } = [];  // New list of FilterOption objects
        public List<int>? NumericCriteria { get; set; } = null; // Numeric filters (e.g., ManaValue, CardsForTrade)
        public string ReadableLabel { get; set; } = string.Empty;

        private string _defaultText = string.Empty;
        public string DefaultText
        {
            get => _defaultText;
            set
            {
                _defaultText = value;
                OnPropertyChanged(nameof(DefaultText)); // Notify UI of updates
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

