using CollectaMundo.DomainLogic.Models;
using System.ComponentModel;

namespace CollectaMundo.ViewModels
{
    public class CardViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        public List<CardSet> Cards { get; set; } = [];

        private List<CardSet> _filteredCards = [];
        public List<CardSet> FilteredCards
        {
            get => _filteredCards;
            set
            {
                if (_filteredCards != value)
                {
                    _filteredCards = value;
                    OnPropertyChanged(nameof(FilteredCards));
                }
            }
        }
    }
}
