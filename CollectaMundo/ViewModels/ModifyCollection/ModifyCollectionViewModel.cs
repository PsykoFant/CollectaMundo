using CollectaMundo.ApplicationServices.ModifyCollection;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.CardLocations.Models;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.DomainLogic.Shared.Models;
using CollectaMundo.ViewModels.ModifyCollection;
using CollectaMundo.ViewModels.Shell;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Text;

namespace CollectaMundo.ViewModels
{
    public partial class ModifyCollectionViewModel : ObservableObject
    {
        private readonly IModifyCollectionService _service;
        private readonly ICardCollectionHost _cardCollectionHost;
        private IReadOnlyList<CardLocation> _availableLocations = [];
        private readonly bool _removeCardWhenZero;

        public event EventHandler<CollectionChangeSet<CardSet>>? CollectionChanged;
        public ObservableCollection<CardSetEditRowViewModel> CardsToAddOrEdit { get; } = [];
        public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);
        public bool ShowCounts => !HasStatus;
        public bool IsCollectionEditVisible => CardsToAddOrEdit.Count != 0 && !HasStatus;
        public IReadOnlyList<CardLocation> AvailableLocations => _availableLocations;
        public ModifyCollectionViewModel(IModifyCollectionService service, ICardCollectionHost cardCollectionHost, bool removeCardWhenZero)
        {
            _cardCollectionHost = cardCollectionHost;
            _service = service;
            _removeCardWhenZero = removeCardWhenZero;


            CardsToAddOrEdit.CollectionChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(IsCollectionEditVisible));
                OnPropertyChanged(nameof(ShowCounts));
            };
        }
        partial void OnStatusMessageChanged(string? oldValue, string newValue)
        {
            OnPropertyChanged(nameof(HasStatus));
            OnPropertyChanged(nameof(ShowCounts));
            OnPropertyChanged(nameof(IsCollectionEditVisible));
        }
        public void ClearStatus()
        {
            StatusMessage = string.Empty;
        }
        public void SetAvailableLocations(IReadOnlyList<CardLocation> availableLocations)
        {
            _availableLocations = availableLocations;

            foreach (var row in CardsToAddOrEdit)
            {
                row.UpdateAvailableLocations(availableLocations);
            }

            RefreshColumnsTrigger++;
        }

        [ObservableProperty]
        private string statusMessage = string.Empty;

        [ObservableProperty]
        private int clearSelectionTrigger;

        [ObservableProperty]
        private int refreshColumnsTrigger;

        // Commands - add to listviews
        [RelayCommand]
        private async Task AddSelectedCards(object param)
        {
            if (param is not IEnumerable<object> selectedItems)
            {
                return;
            }

            StatusMessage = string.Empty;

            foreach (var selected in selectedItems.OfType<CardSet>())
            {
                // service now returns a CardSet ready for editing (clone/defaults applied)
                var editable = await _service.CreateCardForAddAsync(selected);
                if (editable is null)
                {
                    continue;
                }

                CardsToAddOrEdit.Add(new CardSetEditRowViewModel(editable, _availableLocations, RefreshColumnsCommand));
            }

            ClearSelectionTrigger++;
            RefreshColumnsTrigger++;
        }

        [RelayCommand]
        private async Task EditSelectedCards(object param)
        {
            if (param is not IEnumerable<object> selectedItems)
            {
                return;
            }

            StatusMessage = string.Empty;

            foreach (var selected in selectedItems.OfType<CardSet>())
            {
                var editable = await _service.CreateCardForEditAsync(selected);
                if (editable is null)
                {
                    continue;
                }

                CardsToAddOrEdit.Add(new CardSetEditRowViewModel(editable, _availableLocations, RefreshColumnsCommand));
            }

            ClearSelectionTrigger++;
            RefreshColumnsTrigger++;
        }

        [RelayCommand]
        private void ClearCardsToAdd()
        {
            CardsToAddOrEdit.Clear();
            ClearSelectionTrigger++;
        }

        [RelayCommand]
        private void RefreshColumns()
        {
            RefreshColumnsTrigger++;
        }

        [RelayCommand]
        private static void IncrementCount(CardSetEditRowViewModel? row)
        {
            if (row is not null)
            {
                row.CardsOwned++;
            }
        }

        [RelayCommand]
        private void DecrementCount(CardSetEditRowViewModel? row)
        {
            if (row is null || row.CardsOwned <= 0)
            {
                return;
            }

            row.CardsOwned--;

            if (row.CardsOwned < row.CardsForTrade)
            {
                row.CardsForTrade = row.CardsOwned;
            }

            // remove-when-zero without Dispatcher (commands run on UI thread)
            if (_removeCardWhenZero && row.CardsOwned <= 0)
            {
                CardsToAddOrEdit.Remove(row);
            }

            RefreshColumnsTrigger++;
        }

        [RelayCommand]
        private static void IncrementTrade(CardSetEditRowViewModel? row)
        {
            if (row is not null && row.CardsForTrade < row.CardsOwned)
            {
                row.CardsForTrade++;
            }
        }

        [RelayCommand]
        private static void DecrementTrade(CardSetEditRowViewModel? row)
        {
            if (row is not null && row.CardsForTrade > 0)
            {
                row.CardsForTrade--;
            }
        }

        // Submit cards from listview
        [RelayCommand]
        private async Task SubmitNewCardsAsync()
        {
            await SubmitBatchAsync(CardsToAddOrEdit.Select(r => r.CardToAddOrEdit), (cards, snapshot) => _service.SubmitCardBatchAsync(cards, snapshot), clearAfter: true, summaryTitle: "Added the following cards to your collection:");
        }

        [RelayCommand]
        private async Task SubmitCardEditsAsync()
        {
            await SubmitBatchAsync(CardsToAddOrEdit.Select(r => r.CardToAddOrEdit), (cards, snapshot) => _service.SubmitCardBatchAsync(cards, snapshot), clearAfter: true, summaryTitle: "Updated the following cards with these values:");
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

            await SubmitBatchAsync(originals, (cards, snapshot) => _service.SubmitNewCardsWithDefaultsBatchAsync(cards, snapshot), clearAfter: false, summaryTitle: "Added the following cards with default values:");
        }

        [RelayCommand]
        private async Task DeleteSelectedCardsAsync(object? param)
        {
            if (param is not IEnumerable<object> sel)
            {
                return;
            }

            var sourceCards = sel.OfType<CardSetEditRowViewModel>().Select(r => r.CardToAddOrEdit).Cast<CardSet>().Concat(sel.OfType<CardSet>());

            var toDelete = sourceCards
                .Select(o => new CardSet
                {
                    CardId = o.CardId,
                    Name = o.Name,
                    SelectedCondition = o.SelectedCondition,
                    Language = o.Language,
                    SelectedFinish = o.SelectedFinish,
                    CardsOwned = 0,
                }).ToList();

            if (toDelete.Count == 0)
            {
                return;
            }

            await SubmitBatchAsync(toDelete, (cards, snapshot) => _service.SubmitCardBatchAsync(cards, snapshot), clearAfter: true, summaryTitle: "Deleted the following cards from your collection:");
        }

        [RelayCommand]
        private async Task PutAllForTradeAsync(object? param)
        {
            if (param is not IEnumerable<object> sel)
            {
                return;
            }

            var rows = sel.OfType<CardSet>().ToList();
            if (rows.Count == 0)
            {
                return;
            }

            foreach (var r in rows)
            {
                r.CardsForTrade = r.CardsOwned;
            }

            await SubmitBatchAsync(CardsToAddOrEdit.Select(r => r.CardToAddOrEdit), (cards, snapshot) => _service.SubmitCardBatchAsync(cards, snapshot), clearAfter: false, summaryTitle: "Put the following cards up for trade:");
        }

        [RelayCommand]
        private async Task SetNoneForTradeAsync(object? param)
        {
            if (param is not IEnumerable<object> sel)
            {
                return;
            }

            var rows = sel.OfType<CardSet>().ToList();
            if (rows.Count == 0)
            {
                return;
            }

            foreach (var r in rows)
            {
                r.CardsForTrade = 0;
            }

            await SubmitBatchAsync(CardsToAddOrEdit.Select(r => r.CardToAddOrEdit), (cards, snapshot) => _service.SubmitCardBatchAsync(cards, snapshot), clearAfter: false, summaryTitle: "Set the following cards not for trade:");
        }

        // Shared helpers 
        private async Task SubmitBatchAsync(IEnumerable<CardSet> cards, Func<IEnumerable<CardSet>, ICollectionSnapshot, Task<CollectionChangeSet<CardSet>>> submit, bool clearAfter, string summaryTitle)
        {
            var cardList = cards.ToList();
            var snapshot = _cardCollectionHost.CreateMyCollectionSnapshot();

            var changeSet = await submit(cardList, snapshot);

            if (clearAfter)
            {
                CardsToAddOrEdit.Clear();
                ClearSelectionTrigger++;
                OnPropertyChanged(nameof(IsCollectionEditVisible));
            }

            CollectionChanged?.Invoke(this, changeSet);
            StatusMessage = BuildSubmitSummary(changeSet, cardList, summaryTitle);
        }
        private static string BuildSubmitSummary(CollectionChangeSet<CardSet> changeSet, IReadOnlyList<CardSet> submittedCards, string summaryTitle)
        {
            var sb = new StringBuilder();

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

            if (changeSet.RemovedIds.Count > 0)
            {
                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }

                sb.AppendLine("Deleted the following cards from your collection:")
                  .AppendLine();

                var deletedCards = submittedCards
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

            return sb.ToString().TrimEnd();
        }
    }
}
