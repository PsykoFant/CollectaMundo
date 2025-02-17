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

        // Underlying FilterDefaults instance
        public FilterDefaults FilterDefaults { get; }

        public string CriteriaKey => FilterDefaults.CriteriaKey;

        // Directly bind to AllCriteria from FilterDefaults
        public ObservableCollection<string> AvailableOptions { get; }

        public string DefaultText => FilterDefaults.DefaultText;

        public FilterItemViewModel(FilterDefaults filterDefaults)
        {
            FilterDefaults = filterDefaults;
            AvailableOptions = new ObservableCollection<string>(filterDefaults.AllCriteria);
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
