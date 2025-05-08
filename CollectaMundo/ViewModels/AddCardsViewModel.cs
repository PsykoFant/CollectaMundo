using CollectaMundo.ApplicationServices;
using CollectaMundo.DomainLogic.Models;
using CollectaMundo.Utilities;
using ServiceStack;
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

        public event EventHandler<CardProcessedEventArgs>? CardProcessed;
        public ObservableCollection<CardSet> CardsToAdd { get; } = [];

        // Controls removal behavior when CardsOwned reaches zero.
        public bool RemoveCardWhenCardsOwnedZero { get; set; } = true; // Default: remove card

        private readonly IEditCollectionCoordinator _coordinator;

        // Constructor
        public AddCardsViewModel(IEditCollectionCoordinator coordinator)
        {
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
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

            OnPropertyChanged(nameof(CollectionEditVisibility));
        }
        private void Card_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is CardSet card && e.PropertyName == nameof(CardSet.CardsOwned))
            {
                // Only remove the card if the flag is true.
                if (RemoveCardWhenCardsOwnedZero && card.CardsOwned <= 0 && CardsToAdd.Contains(card))
                {
                    // Remove on the UI thread.
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

        // Controls visibility of the listviews.
        public Visibility CollectionEditVisibility => CardsToAdd.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        // Commands - add to listviews
        public ICommand AddSelectedCardsCommand => new RelayCommand<object>(async param =>
        {
            if (param is IEnumerable<object> selectedItems)
            {
                var cards = selectedItems.OfType<CardSet>();
                foreach (var card in cards)
                {
                    // Call the manager to add the card to the in-memory collection.
                    await _coordinator.AddCardToAddCardsListViewAsync(card, CardsToAdd);
                }

                // Increment the trigger to signal the view to clear selection.
                ClearSelectionTrigger++;

                // Now increment the trigger to signal the view to refresh columns.
                RefreshColumnsTrigger++;

                StatusMessage = String.Empty; // Clear status message
            }
        });
        public ICommand EditSelectedCardsCommand => new RelayCommand<object>(async param =>
        {
            if (param is IEnumerable<object> selectedItems)
            {
                var cards = selectedItems.OfType<CardSet>();
                foreach (var card in cards)
                {
                    // Call the manager to add the card to the in-memory collection.
                    await _coordinator.AddCardToEditCardsListViewAsync(card, CardsToAdd);
                }

                // Increment the trigger to signal the view to clear selection.
                ClearSelectionTrigger++;

                // Now increment the trigger to signal the view to refresh columns.
                RefreshColumnsTrigger++;
            }
        });

        // Commands - manipulate listviews
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
        public static ICommand IncrementCountCommand => new RelayCommand<object>(param =>
        {
            if (param is CardSet card)
            {
                card.CardsOwned++;
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
            }
        });
        public static ICommand IncrementTradeCommand => new RelayCommand<object>(param =>
        {
            if (param is CardSet card)
            {
                if (card.CardsForTrade < card.CardsOwned)
                {
                    card.CardsForTrade++;
                }
            }
        });
        public static ICommand DecrementTradeCommand => new RelayCommand<object>(param =>
        {
            if (param is CardSet card)
            {
                card.CardsForTrade--;
            }
        });

        // Commands - submit cards from listview
        // 1) The shared helper:
        private async Task SubmitCardsAsync(IEnumerable<CardSet> toSubmit, Func<CardSet, Task<CardSet>> persistAndFetch, bool clearAfter = true, string summaryTitle = "Added the following cards to your collection:")
        {
            var originals = toSubmit.ToList();
            var persisted = new List<CardSet>();

            // 1a) Persist each one & raise the event
            foreach (var o in originals)
            {
                var saved = await persistAndFetch(o);
                CardProcessed?.Invoke(this, new CardProcessedEventArgs(saved));
                persisted.Add(saved);
            }

            // 1b) Clear the “to add” pane if requested
            if (clearAfter)
            {
                CardsToAdd.Clear();
                ClearSelectionTrigger++;
            }

            // 1c) Build your single summary string
            StatusMessage = summaryTitle + "\n\n"
                + string.Join("\n", persisted.Select(c =>
                    $"- {c.Name} (Condition: {c.SelectedCondition}, " +
                    $"Language: {c.Language}, Finish: {c.SelectedFinish}, " +
                    $"Owned: {c.CardsOwned}, Trade: {c.CardsForTrade})"));
        }

        // 2) Now each command is just a thin call into that helper:

        public ICommand SubmitNewCardsCommand => new RelayCommand<object>(async _ =>
            await SubmitCardsAsync(
                CardsToAdd,
                card => _coordinator.SubmitCollectionUpdatesAsync(card, isEdit: false)
            )
        );

        public ICommand SubmitCardEditsCommand => new RelayCommand<object>(async _ => await SubmitCardsAsync(CardsToAdd, card => _coordinator.SubmitCollectionUpdatesAsync(card, isEdit: true), true, "Updated the following cards withthese values:")
        );

        public ICommand SubmitNewCardsWithDefaultsCommand => new RelayCommand<object>(async param =>
        {
            if (param is not IEnumerable<object> sel)
            {
                return;
            }

            var originals = sel.OfType<CardSet>();
            if (!originals.Any())
            {
                return;
            }

            await SubmitCardsAsync(
                originals,
                card => _coordinator.SubmitNewCardsWithDefaultsAsync(card, isEdit: false),
                clearAfter: false,                        // maybe you don’t want to clear CardsToAdd here
                summaryTitle: "Added the following cards with default values:"
            );
        });

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage != value)
                {
                    _statusMessage = value;
                    OnPropertyChanged(nameof(StatusMessage));
                    OnPropertyChanged(nameof(StatusVisibility));
                }
            }
        }

        // Collapse when no message
        public Visibility StatusVisibility
            => string.IsNullOrEmpty(StatusMessage)
                ? Visibility.Collapsed
                : Visibility.Visible;

    }
    public class CardProcessedEventArgs(CardSet card) : EventArgs
    {
        public CardSet Card { get; } = card;
    }
}
