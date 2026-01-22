using CollectaMundo.ApplicationServices.EditCollection;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.ViewModels.Import;
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
        private readonly IParentViewModelContext _parentContext;
        private readonly bool _removeCardWhenZero;

        // Constructor
        public EditCollectionViewModel(IEditCollectionService service, IParentViewModelContext parentContext, bool removeCardWhenZero)
        {
            _parentContext = parentContext;
            _service = service;
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
            await SubmitBatchAsync(CardsToAdd,(cards, snapshot) => 
            _service.SubmitCardBatchAsync(cards, snapshot),
            clearAfter: true,summaryTitle: "Added the following cards to your collection:");
        }

        [RelayCommand]
        private async Task SubmitCardEditsAsync()
        {
            await SubmitBatchAsync(CardsToAdd, (cards, snapshot) =>
            _service.SubmitCardBatchAsync(cards, snapshot),
            clearAfter: true, summaryTitle: "Updated the following cards with these values:");
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

            await SubmitBatchAsync(CardsToAdd, (cards, snapshot) =>
            _service.SubmitCardBatchAsync(cards, snapshot),
            clearAfter: false, summaryTitle: "Added the following cards with default values:");
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
            
            await SubmitBatchAsync(CardsToAdd, (cards, snapshot) =>
            _service.SubmitCardBatchAsync(cards, snapshot),
            clearAfter: true, summaryTitle: "Deleted the following cards from your collection:");
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

            await SubmitBatchAsync(CardsToAdd, (cards, snapshot) =>
            _service.SubmitCardBatchAsync(cards, snapshot),
            clearAfter: false, summaryTitle: "Put the following cards up for trade:");
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

            await SubmitBatchAsync(CardsToAdd, (cards, snapshot) =>
            _service.SubmitCardBatchAsync(cards, snapshot),
            clearAfter: false, summaryTitle: "Set the following cards not for trade:");
        }

        // Shared helper
        private async Task SubmitBatchAsync(IEnumerable<CardSet> cards,
            Func<IEnumerable<CardSet>, ICollectionSnapshot, Task<CollectionChangeSet<CardSet>>> submit,
            bool clearAfter,string summaryTitle)
        {
            var snapshot = _parentContext.CreateMyCollectionSnapshot();

            // 1) Submit to service
            var changeSet = await submit(cards, snapshot);

            // 2) Clear UI selection if requested
            if (clearAfter)
            {
                CardsToAdd.Clear();
                ClearSelectionTrigger++;
            }

            // 3) Apply in-memory collection changes
            CollectionChanged?.Invoke(this, changeSet);

            // 4) CreateCollectionChangeSetFromEdits user-facing summary
            var sb = new StringBuilder();

            // ---- Upserts (added or updated cards)
            if (changeSet.AddedOrUpdated.Count > 0)
            {
                sb.AppendLine(summaryTitle).AppendLine();

                foreach (var c in changeSet.AddedOrUpdated)
                {
                    sb.AppendLine(
                        $"- {c.Name} " +
                        $"(Condition: {c.SelectedCondition}, " +
                        $"Language: {c.Language}, " +
                        $"Finish: {c.SelectedFinish}, " +
                        $"Owned: {c.CardsOwned}, " +
                        $"Trade: {c.CardsForTrade})");
                }
            }

            // ---- Deletions
            if (changeSet.RemovedIds.Count > 0)
            {
                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }

                sb.AppendLine("Deleted the following cards from your collection:")
                  .AppendLine();

                // Best-effort lookup from originals (safe + sufficient for UI)
                var deletedCards = cards
                    .Where(c => c.CardId.HasValue && changeSet.RemovedIds.Contains(c.CardId.Value))
                    .ToList();

                foreach (var c in deletedCards)
                {
                    sb.AppendLine(
                        $"- {c.Name} " +
                        $"(Condition: {c.SelectedCondition}, " +
                        $"Language: {c.Language}, " +
                        $"Finish: {c.SelectedFinish})");
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

        public Visibility StatusVisibility => string.IsNullOrEmpty(StatusMessage)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }
}
