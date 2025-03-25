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


        // Countertrigger to clear datagrid selection
        private int _clearSelectionTrigger;
        public int ClearSelectionTrigger
        {
            get => _clearSelectionTrigger;
            set
            {
                if (_clearSelectionTrigger != value)
                {
                    _clearSelectionTrigger = value;
                    OnPropertyChanged(nameof(ClearSelectionTrigger));
                }
            }
        }

        // Countertrigger to resize listview columns
        private int _refreshColumnsTrigger;
        public int RefreshColumnsTrigger
        {
            get => _refreshColumnsTrigger;
            set
            {
                if (_refreshColumnsTrigger != value)
                {
                    _refreshColumnsTrigger = value;
                    OnPropertyChanged(nameof(RefreshColumnsTrigger));
                }
            }
        }


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

                // Increment the trigger to signal the view to clear selection.
                ClearSelectionTrigger++;

                // Await layout processing.
                await Application.Current.Dispatcher.InvokeAsync(() => { },
                    System.Windows.Threading.DispatcherPriority.Render);

                // Now increment the trigger to signal the view to refresh columns.
                RefreshColumnsTrigger++;
            }
        });

        public ICommand ClearCardsToAddCommand => new RelayCommand<object>(param =>
        {
            // Clear the in-memory collection.
            CardsToAdd.Clear();

            // Hide the add cards list area.
            CardsToAddVisibility = Visibility.Collapsed;

            // Increment the trigger to signal the view to clear selection.
            ClearSelectionTrigger++;
        });

        public ICommand RefreshColumnsCommand => new RelayCommand<object>(param =>
        {
            RefreshColumnsTrigger++;
        });


    }
}
