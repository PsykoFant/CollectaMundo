using CollectaMundo.ApplicationServices.EditCollection;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.ViewModels.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ServiceStack;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows;

namespace CollectaMundo.ViewModels
{
    public partial class EditCollectionViewModel : ObservableObject
    {
        public event EventHandler<CollectionChangeSet<CardSet>>? CollectionChanged;
        public ObservableCollection<CardSet> CardsToAdd { get; } = [];

        private readonly IEditCollectionService _service;
        private readonly ICollectionChangeApplier<CardSet> _collectionChangeApplier;
        private readonly bool _removeCardWhenZero;


        // Constructor
        public EditCollectionViewModel(IEditCollectionService service, ICollectionChangeApplier<CardSet> collectionChangeApplier, bool removeCardWhenZero)
        {
            _service = service;
            _collectionChangeApplier = collectionChangeApplier;
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
        [ObservableProperty]
        private int clearSelectionTrigger;

        // Countertrigger to resize listview columns
        [ObservableProperty]
        private int refreshColumnsTrigger;

        // Controls visibility of the listviews.
        public Visibility CollectionEditVisibility => CardsToAdd.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        // Commands - add to listviews
        [RelayCommand]
        private async Task AddSelectedCards(object param)
        {
            if (param is IEnumerable<object> selectedItems)
            {
                StatusMessage = string.Empty;

                var cards = selectedItems.OfType<CardSet>();
                foreach (var card in cards)
                {
                    await _service.AddCardToAddCardsListViewAsync(card, CardsToAdd);
                }

                ClearSelectionTrigger++;
                RefreshColumnsTrigger++;
            }
        }
        [RelayCommand]
        private async Task EditSelectedCards(object param)
        {
            if (param is IEnumerable<object> selectedItems)
            {
                StatusMessage = String.Empty; // Clear status message

                var cards = selectedItems.OfType<CardSet>();
                foreach (var card in cards)
                {
                    // Call the manager to add the card to the in-memory collection.
                    await _service.AddCardToEditCardsListViewAsync(card, CardsToAdd);
                }

                // Increment the trigger to signal the view to clear selection.
                ClearSelectionTrigger++;

                // Now increment the trigger to signal the view to refresh columns.
                RefreshColumnsTrigger++;
            }
        }

        // Commands - manipulate listviews
        [RelayCommand]
        private void ClearCardsToAdd()
        {
            // Clear the in-memory collection.
            CardsToAdd.Clear();

            // Increment the trigger to signal the view to clear selection.
            ClearSelectionTrigger++;
        }
        [RelayCommand]
        private void RefreshColumns()
        {
            RefreshColumnsTrigger++;
        }

        [RelayCommand]
        private static void IncrementCount(CardSet? card)
        {
            if (card is not null)
            {
                card.CardsOwned++;
            }
        }

        [RelayCommand]
        private void DecrementCount(CardSet? card)
        {
            if (card is not null && card.CardsOwned > 0)
            {
                card.CardsOwned--;

                if (card.CardsOwned < card.CardsForTrade)
                {
                    card.CardsForTrade = card.CardsOwned;
                }

                RefreshColumnsTrigger++;
            }
        }

        [RelayCommand]
        private static void IncrementTrade(CardSet? card)
        {
            if (card is not null && card.CardsForTrade < card.CardsOwned)
            {
                card.CardsForTrade++;
            }
        }

        [RelayCommand]
        private static void DecrementTrade(CardSet? card)
        {
            if (card is not null)
            {
                card.CardsForTrade--;
            }
        }


        // Commands - submit cards from listview
        [RelayCommand]
        private async Task SubmitNewCardsAsync()
        {
            await SubmitBatchAsync(
                CardsToAdd,
                cards => _service.SubmitCardBatchAsync(cards),
                clearAfter: true,
                summaryTitle: "Added the following cards to your collection:");
        }

        [RelayCommand]
        private async Task SubmitCardEditsAsync()
        {
            await SubmitBatchAsync(
                CardsToAdd,
                cards => _service.SubmitCardBatchAsync(cards),
                clearAfter: true,
                summaryTitle: "Updated the following cards with these values:");
        }

        [RelayCommand]
        private async Task SubmitNewCardsWithDefaults(object? param)
        {
            if (param is not IEnumerable<object> sel)
            {
                return;
            }

            var originals = sel.OfType<CardSet>().ToList();
            if (originals.Count == 0)
            {
                return;
            }

            await SubmitBatchAsync(
                originals,
                cards => _service.SubmitNewCardsWithDefaultsBatchAsync(cards),
                clearAfter: false,
                summaryTitle: "Added the following cards with default values:");
        }

        [RelayCommand]
        private async Task DeleteSelectedCardsAsync(object? param)
        {
            if (param is not IEnumerable<object> sel)
            {
                return;
            }

            var originals = sel.OfType<CardSet>().ToList();
            if (originals.Count == 0)
            {
                return;
            }

            var toDelete = originals.Select(o => new CardSet
            {
                CardId = o.CardId,
                Name = o.Name,
                SelectedCondition = o.SelectedCondition,
                Language = o.Language,
                SelectedFinish = o.SelectedFinish,
                CardsOwned = 0,
            }).ToList();

            await SubmitBatchAsync(
                toDelete,
                cards => _service.SubmitCardBatchAsync(cards),
                clearAfter: true,
                summaryTitle: "Deleted the following cards from your collection:");
        }

        [RelayCommand]
        private async Task PutAllForTradeAsync(object? param)
        {
            if (param is not IEnumerable<object> sel)
            {
                return;
            }

            var cards = sel.OfType<CardSet>().ToList();
            if (cards.Count == 0)
            {
                return;
            }

            foreach (var c in cards)
            {
                c.CardsForTrade = c.CardsOwned;
            }

            await SubmitBatchAsync(cards, cards => _service.SubmitCardBatchAsync(cards), clearAfter: false, summaryTitle: "Put the following cards up for trade:");
        }

        [RelayCommand]
        private async Task SetNoneForTradeAsync(object? param)
        {
            if (param is not IEnumerable<object> sel)
            {
                return;
            }

            var cards = sel.OfType<CardSet>().ToList();
            if (cards.Count == 0)
            {
                return;
            }

            foreach (var c in cards)
            {
                c.CardsForTrade = 0;
            }

            await SubmitBatchAsync(
                cards,
                cards => _service.SubmitCardBatchAsync(cards),
                clearAfter: false,
                summaryTitle: "Set the following cards not for trade:");
        }


        // Shared helper
        private async Task SubmitBatchAsync(IEnumerable<CardSet> originals, Func<IEnumerable<CardSet>, Task<List<CardChangeEventArgs>>> persistBatch, bool clearAfter, string summaryTitle)
        {
            var list = originals.ToList();
            var changes = await persistBatch(list);

            if (clearAfter)
            {
                CardsToAdd.Clear();
                ClearSelectionTrigger++;
            }

            // 3) Build and apply collection changes
            var changeSet = CollectionChangeBuilder.Build(changes);
            CollectionChanged?.Invoke(this, changeSet);

            // 4) Build summary
            var sb = new StringBuilder();

            var ups = changes.Where(c => c.Type == CardChangeEventArgs.ChangeType.Upsert).Select(c => c.Survivor!).ToList();
            if (ups.Count != 0)
            {
                sb.AppendLine(summaryTitle).AppendLine();
                foreach (var c in ups)
                {
                    sb.AppendLine($"- {c.Name} (Condition: {c.SelectedCondition}, Language: {c.Language}, Finish: {c.SelectedFinish}, Owned: {c.CardsOwned}, Trade: {c.CardsForTrade})");
                }
            }

            var deletedIds = System.Linq.Enumerable.ToHashSet(changes.Where(ch => ch.Type == CardChangeEventArgs.ChangeType.Delete).SelectMany(ch => ch.Removed));
            var deletedCards = list.Where(c => c.CardId.HasValue && deletedIds.Contains(c.CardId.Value)).ToList();
            if (deletedCards.Count != 0)
            {
                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }

                sb.AppendLine("Deleted the following cards from your collection:").AppendLine();
                foreach (var c in deletedCards)
                {
                    sb.AppendLine($"- {c.Name} (Condition: {c.SelectedCondition}, Language: {c.Language}, Finish: {c.SelectedFinish})");
                }
            }

            StatusMessage = sb.ToString().TrimEnd();
        }

        [ObservableProperty]
        private string statusMessage = string.Empty;
        partial void OnStatusMessageChanged(string? oldValue, string newValue)
        {
            OnPropertyChanged(nameof(StatusVisibility));
        }

        public Visibility StatusVisibility
            => string.IsNullOrEmpty(StatusMessage)
                ? Visibility.Collapsed
                : Visibility.Visible;
    }
}
