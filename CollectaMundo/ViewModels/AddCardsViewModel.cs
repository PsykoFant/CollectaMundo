using CollectaMundo.Managers;
using CollectaMundo.Models;
using CollectaMundo.Services;
using CollectaMundo.Utilities;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace CollectaMundo.ViewModels
{
    public class AddCardsViewModel : INotifyPropertyChanged
    {
        // INotifyPropertyChanged implementation...
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        // Observable collection bound to the ListView (CardsToAddListView)
        public ObservableCollection<CardSet> CardsToAdd { get; } = [];

        // Business logic manager (injected or instantiated here)
        private readonly CardCollectionManager _cardCollectionManager;

        public AddCardsViewModel(ICardCollectionService cardCollectionService)
        {
            _cardCollectionManager = new CardCollectionManager(cardCollectionService);
            CardsToAdd.Add(new CardSet { Name = "Test Card", SetName = "Test Set", Uuid = "dummy" });
        }

        // Command to add selected cards from the DataGrid to the listview.
        public ICommand AddSelectedCardsCommand => new RelayCommand<object>(async param =>
        {
            // Assuming the parameter is bound to SelectedItems from the DataGrid.
            if (param is IEnumerable<object> selectedItems)
            {
                var cards = selectedItems.OfType<CardSet>();
                foreach (var card in cards)
                {
                    await _cardCollectionManager.AddCardToListViewAsync(card, CardsToAdd);
                }
            }
        });
    }
}
