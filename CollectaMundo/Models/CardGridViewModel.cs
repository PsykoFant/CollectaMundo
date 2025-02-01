using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;

namespace CollectaMundo.Models
{
    public class CardGridViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<CardSet> _filteredCards = new();
        public ObservableCollection<CardSet> FilteredCards
        {
            get => _filteredCards;
            set
            {
                _filteredCards = value;
                OnPropertyChanged(nameof(FilteredCards));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // ✅ Update FilteredCards efficiently
        public void UpdateData(IEnumerable<CardSet> newCards)
        {
            _filteredCards.Clear();
            foreach (var card in newCards)
            {
                _filteredCards.Add(card);
            }
        }

        public void ApplyFilter(IEnumerable<CardSet> allCards)
        {
            Stopwatch sw = Stopwatch.StartNew();

            var filterCriteria = MainWindow.CurrentInstance.filterSelections
                .Select(fs => fs.ToFilterCriteria())
                .ToList();

            var validFilters = filterCriteria
                .Where(filter => PropertyExistsInList(filter.CriteriaKey, allCards))
                .ToList();

            var filteredResults = (validFilters.Count == 0)
                ? allCards.ToList()
                : allCards.Where(card => validFilters.All(filter => filter.Matches(card))).ToList();

            // 🔥 Efficiently update ObservableCollection without performance bottlenecks
            UpdateData(filteredResults);

            sw.Stop();
            Debug.WriteLine($"Time for applyfilter: {sw.ElapsedMilliseconds}");
        }

        private static bool PropertyExistsInList(string? criteriaKey, IEnumerable<CardSet> cards)
        {
            if (string.IsNullOrEmpty(criteriaKey)) return false;
            if (!MainWindow.CurrentInstance.CriteriaKeyToPropertyMap.TryGetValue(criteriaKey, out var propertyName))
                return false;

            return cards.Any(card => card.GetType().GetProperty(propertyName) != null);
        }
    }

}
