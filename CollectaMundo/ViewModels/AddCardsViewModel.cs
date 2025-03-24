using CollectaMundo.Managers;
using CollectaMundo.Models;
using CollectaMundo.Services;
using CollectaMundo.Utilities;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace CollectaMundo.ViewModels
{
    public class AddCardsViewModel(ICardCollectionService cardCollectionService) : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        // Collection bound to the ListView.
        public ObservableCollection<CardSet> CardsToAdd { get; } = new ObservableCollection<CardSet>();

        // Controls visibility of the CardsToAdd listview.
        private Visibility _cardsToAddVisibility = Visibility.Collapsed;
        public Visibility CardsToAddVisibility
        {
            get => _cardsToAddVisibility;
            set
            {
                if (_cardsToAddVisibility != value)
                {
                    _cardsToAddVisibility = value;
                    OnPropertyChanged(nameof(CardsToAddVisibility));
                }
            }
        }

        // Business logic manager.
        private readonly CardCollectionManager _cardCollectionManager = new(cardCollectionService);

        // Command to add selected cards from the DataGrid.
        public ICommand AddSelectedCardsCommand => new RelayCommand<object>(async param =>
        {
            if (param is IEnumerable<object> selectedItems)
            {
                var cards = selectedItems.OfType<CardSet>();
                foreach (var card in cards)
                {
                    // Call the manager to add the card to the in-memory collection.
                    await _cardCollectionManager.AddCardToListViewAsync(card, CardsToAdd);
                }
                // After processing, make the listview visible.
                CardsToAddVisibility = Visibility.Visible;
            }
        });

        public ICommand ClearCardsToAddCommand => new RelayCommand<object>(async param =>
        {
            // Clear the in-memory collection.
            CardsToAdd.Clear();

            // Hide the add cards list area.
            CardsToAddVisibility = Visibility.Collapsed;

            // Optionally, update other UI state properties if you exposed them,
            // such as for a Submit button or a logo.
        });

    }
}
