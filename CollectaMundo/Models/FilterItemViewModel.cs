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

        // The category of the filter (e.g., "Rarity", "Types", etc.)
        public string CriteriaKey { get; }

        // Available filtering options for this category
        public ObservableCollection<string> AvailableOptions { get; } = new();

        public FilterItemViewModel(string criteriaKey)
        {
            CriteriaKey = criteriaKey;
        }

        // Debug method to verify correct initialization
        public void DebugFilterItem()
        {
            Debug.WriteLine($"===== DEBUG: Filter Item ({CriteriaKey}) =====");
            Debug.WriteLine($"Available Options: {string.Join(", ", AvailableOptions)}");
            Debug.WriteLine($"====================================");
        }
    }
}
