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
    public class AddCardsViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public ObservableCollection<CardSet> CardsToAdd { get; } = new ObservableCollection<CardSet>();

        private readonly CardCollectionManager _cardCollectionManager;

        // Primary constructor body
        public AddCardsViewModel(ICardCollectionService cardCollectionService)
        {
            _cardCollectionManager = new CardCollectionManager(cardCollectionService);
            CardsToAdd.CollectionChanged += CardsToAdd_CollectionChanged;
        }
        private void CardsToAdd_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // When new items are added, subscribe to their PropertyChanged event.
            if (e.NewItems != null)
            {
                foreach (var newItem in e.NewItems)
                {
                    if (newItem is CardSet card)
                    {
                        card.PropertyChanged += Card_PropertyChanged;
                    }
                }
            }
            // When items are removed, unsubscribe.
            if (e.OldItems != null)
            {
                foreach (var oldItem in e.OldItems)
                {
                    if (oldItem is CardSet card)
                    {
                        card.PropertyChanged -= Card_PropertyChanged;
                    }
                }
            }
        }
        private void Card_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is CardSet card && e.PropertyName == nameof(CardSet.CardsOwned))
            {
                // If CardsOwned is zero, remove the card from the collection.
                if (card.CardsOwned <= 0 && CardsToAdd.Contains(card))
                {
                    // Removal must be done on the UI thread.
                    Application.Current.Dispatcher.Invoke(() => CardsToAdd.Remove(card));
                }
            }
        }

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
            //System.Windows.Data.CollectionViewSource.GetDefaultView(CardsToAdd).Refresh(); OnPropertyChanged(nameof(CardsToAdd));
            RefreshColumnsTrigger++;
        });

        public ICommand IncrementCountCommand => new RelayCommand<object>(param =>
        {
            if (param is CardSet card)
            {
                card.CardsOwned++;
                System.Windows.Data.CollectionViewSource.GetDefaultView(CardsToAdd).Refresh(); OnPropertyChanged(nameof(CardsToAdd));
            }
        });
        public ICommand DecrementCountCommand => new RelayCommand<object>(param =>
        {
            if (param is CardSet card)
            {
                if (card.CardsOwned > 0)
                {
                    card.CardsOwned--;
                    // Remove the card if the count reaches zero.
                    if (card.CardsOwned == 0)
                    {
                        CardsToAdd.Remove(card);
                        RefreshColumnsTrigger++;
                        if (CardsToAdd.Count == 0)
                        {
                            CardsToAddVisibility = Visibility.Collapsed;
                        }
                    }
                }
                System.Windows.Data.CollectionViewSource.GetDefaultView(CardsToAdd).Refresh(); OnPropertyChanged(nameof(CardsToAdd));
            }
        });
    }
}
