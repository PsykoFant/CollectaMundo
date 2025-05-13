using CollectaMundo.ApplicationServices;
using CollectaMundo.DomainLogic.Models;
using CollectaMundo.Utilities;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace CollectaMundo.ViewModels
{
    public class EditCollectionViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        //public event EventHandler<CardProcessedEventArgs>? CardProcessed;
        public event EventHandler<CardChangeEventArgs>? CardChanged;

        public ObservableCollection<CardSet> CardsToAdd { get; } = [];

        private readonly IEditCollectionCoordinator _coordinator;
        private readonly bool _removeCardWhenZero;
        // Constructor
        public EditCollectionViewModel(IEditCollectionCoordinator coordinator, bool removeCardWhenZero)
        {
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _removeCardWhenZero = removeCardWhenZero;
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
                if (_removeCardWhenZero && card.CardsOwned <= 0 && CardsToAdd.Contains(card))
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
        public ICommand SubmitNewCardsCommand => new RelayCommand<object>(async _ => await SubmitCardsAsync(CardsToAdd, card => _coordinator.SubmitCollectionUpdatesAsync(card, isEdit: false)));
        public ICommand SubmitCardEditsCommand => new RelayCommand<object>(async _ => await SubmitCardsAsync(CardsToAdd, card => _coordinator.SubmitCollectionUpdatesAsync(card, isEdit: true), true, "Updated the following cards with these values:"));
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

            await SubmitCardsAsync(originals, card => _coordinator.SubmitNewCardsWithDefaultsAsync(card), clearAfter: false, summaryTitle: "Added the following cards with default values:");
        });
        public ICommand DeleteSelectedCardsCommand => new RelayCommand<object>(async param =>
        {
            if (param is not IEnumerable<object> sel) return;

            // 1) Pull out the selected CardSet instances
            var originals = sel.OfType<CardSet>().ToList();
            if (originals.Count == 0) return;

            // 2) Clone each one and force CardsOwned=0 (so we don’t stomp on the ListView’s binding)
            var toDelete = originals
              .Select(o => new CardSet
              {
                  CardId = o.CardId,
                  Name = o.Name,
                  SelectedCondition = o.SelectedCondition,
                  Language = o.Language,
                  SelectedFinish = o.SelectedFinish,
                  CardsOwned = 0,
              }).ToList();

            // 3) Reuse your SubmitCardsAsync helper,  
            await SubmitCardsAsync(toDelete, card => _coordinator.SubmitCollectionUpdatesAsync(card, isEdit: true), clearAfter: true, summaryTitle: "Deleted the following cards from your collection:");
        });
        public ICommand PutAllForTradeCommand => new RelayCommand<object>(async param =>
        {
            if (param is not IEnumerable<object> sel) return;

            // 1) Grab the selected CardSet instances
            var cards = sel.OfType<CardSet>().ToList();
            if (cards.Count == 0) return;

            // 2) Mutate each one in-memory
            foreach (var c in cards)
            {
                c.CardsForTrade = c.CardsOwned;
            }

            // 3) Call SubmitCardsAsync helper to persist & raise events
            await SubmitCardsAsync(cards, card => _coordinator.SubmitCollectionUpdatesAsync(card, isEdit: true), clearAfter: false, summaryTitle: "Put the following cards up for trade:");
        });
        public ICommand SetNoneForTradeCommand => new RelayCommand<object>(async param =>
        {
            if (param is not IEnumerable<object> sel) return;

            // 1) Grab the selected CardSet instances
            var cards = sel.OfType<CardSet>().ToList();
            if (cards.Count == 0) return;

            // 2) Mutate each one in-memory
            foreach (var c in cards)
            {
                c.CardsForTrade = 0;
            }

            // 3) Call SubmitCardsAsync helper to persist & raise events
            await SubmitCardsAsync(cards, card => _coordinator.SubmitCollectionUpdatesAsync(card, isEdit: true), clearAfter: false, summaryTitle: "Set the following cards not for trade:");
        });

        // Shared helper
        private async Task SubmitCardsAsync(IEnumerable<CardSet> toSubmit, Func<CardSet, Task<CardChangeEventArgs>> persistAndFetch, bool clearAfter = true, string summaryTitle = "Added the following cards to your collection:")
        {
            var originals = CardsToAdd.ToList();
            var batch = originals.Select(c => (c, isEdit: false));
            var changes = await _coordinator.SubmitCollectionBatchAsync(batch);

            // 2) Clear the "to add" pane if requested
            if (clearAfter)
            {
                CardsToAdd.Clear();
                ClearSelectionTrigger++;
            }

            // 3) Fire CardProcessed events so the UI list updates
            foreach (var change in changes)
            {
                CardChanged?.Invoke(this, change);
            }

            // 4) Build separate lists of survivors (upserts) and deletions
            var survivors = changes
                .Where(ch => ch.Type == CardChangeEventArgs.ChangeType.Upsert)
                .Select(ch => ch.Survivor!)
                .ToList();

            // For deletions, pull the original CardSet(s) that matched the deleted IDs
            var deletedIds = changes
                .Where(ch => ch.Type == CardChangeEventArgs.ChangeType.Delete)
                .SelectMany(ch => ch.Removed)
                .ToHashSet();

            var deletedCards = originals.Where(o => o.CardId.HasValue && deletedIds.Contains(o.CardId.Value)).ToList();


            // 5) Build the summary string
            var sb = new StringBuilder();

            if (survivors.Count != 0)
            {
                sb.AppendLine(summaryTitle);
                sb.AppendLine();
                foreach (var c in survivors)
                {
                    sb.AppendLine($"- {c.Name} (Condition: {c.SelectedCondition}, " +
                                  $"Language: {c.Language}, Finish: {c.SelectedFinish}, " +
                                  $"Owned: {c.CardsOwned}, Trade: {c.CardsForTrade})");
                }
            }

            if (deletedCards.Count != 0)
            {
                if (sb.Length > 0) sb.AppendLine(); // blank line between sections
                sb.AppendLine("Deleted the following cards from your collection:");
                sb.AppendLine();
                foreach (var c in deletedCards)
                {
                    sb.AppendLine($"- {c.Name} (Condition: {c.SelectedCondition}, " +
                                  $"Language: {c.Language}, Finish: {c.SelectedFinish})");
                }
            }

            StatusMessage = sb.ToString().TrimEnd();
        }

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
}
