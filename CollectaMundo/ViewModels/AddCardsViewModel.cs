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
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public ObservableCollection<CardSet> CardsToAdd { get; } = [];
        //public ObservableCollectionEx<CardSet> CardsToAdd { get; } = new ObservableCollectionEx<CardSet>();


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

            OnPropertyChanged(nameof(CardsToAddVisibility));
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
        public Visibility CardsToAddVisibility => CardsToAdd.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        // Commands
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

            // Increment the trigger to signal the view to clear selection.
            ClearSelectionTrigger++;
        });
        public ICommand RefreshColumnsCommand => new RelayCommand<object>(param =>
        {
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
                    RefreshColumnsTrigger++;
                }
                System.Windows.Data.CollectionViewSource.GetDefaultView(CardsToAdd).Refresh(); OnPropertyChanged(nameof(CardsToAdd));
            }
        });
        public ICommand IncrementTradeCommand => new RelayCommand<object>(param =>
        {
            if (param is CardSet card)
            {
                if (card.CardsForTrade < card.CardsOwned)
                {
                    card.CardsForTrade++;
                    System.Windows.Data.CollectionViewSource.GetDefaultView(CardsToAdd).Refresh(); OnPropertyChanged(nameof(CardsToAdd));
                }
            }
        });
        public ICommand DecrementTradeCommand => new RelayCommand<object>(param =>
        {
            if (param is CardSet card)
            {
                card.CardsForTrade--;
                System.Windows.Data.CollectionViewSource.GetDefaultView(CardsToAdd).Refresh(); OnPropertyChanged(nameof(CardsToAdd));
            }
        });
    }
}
