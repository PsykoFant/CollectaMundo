using CollectaMundo.ApplicationServices.ModifyCollection;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.CardLocations.Models;
using CollectaMundo.DomainLogic.CollectionMutations.Models;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.DomainLogic.Shared.CollectionSnapshot;
using CollectaMundo.DomainLogic.Shared.Factories;
using CollectaMundo.DomainLogic.Shared.Models;
using CollectaMundo.Infrastructure.CardLists.Models;
using CollectaMundo.ViewModels.ModifyCollection.ParameterModels;
using CollectaMundo.ViewModels.Shell;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Text;

namespace CollectaMundo.ViewModels.ModifyCollection
{
    public partial class ModifyCollectionViewModel : ObservableObject
    {
        private readonly IModifyCollectionService _service;
        private readonly ICardCollectionHost _cardCollectionHost;
        private IReadOnlyList<CardLocation> _availableLocations = [];
        private readonly Func<string> _getSellPriceDisplayCurrency;
        private readonly Func<decimal?> _getUsdToEurRate;
        private readonly bool _removeCardWhenZero;

        public event EventHandler<CollectionChangeSet<CollectionCardDbRow>>? CollectionChanged;
        public ObservableCollection<CollectionCardDraftRowViewModel> CardsToAddOrEdit { get; } = [];
        public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);
        public bool ShowCounts => !HasStatus;
        public bool IsCollectionEditVisible => CardsToAddOrEdit.Count != 0 && !HasStatus;
        public IReadOnlyList<CardLocation> AvailableLocations => _availableLocations;
        public IReadOnlyList<LocationMenuItemViewModel> LocationMenuItems { get; private set; } = [];

        // Constructor
        public ModifyCollectionViewModel(IModifyCollectionService service, ICardCollectionHost cardCollectionHost, bool removeCardWhenZero, Func<string> getSellPriceDisplayCurrency, Func<decimal?> getUsdToEurRate)
        {
            _cardCollectionHost = cardCollectionHost;
            _service = service;
            _removeCardWhenZero = removeCardWhenZero;
            _getSellPriceDisplayCurrency = getSellPriceDisplayCurrency;
            _getUsdToEurRate = getUsdToEurRate;

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

            LocationMenuItems = BuildLocationMenuItems(availableLocations);
            OnPropertyChanged(nameof(LocationMenuItems));

            foreach (var row in CardsToAddOrEdit)
            {
                row.UpdateAvailableLocations(availableLocations);
            }

            RefreshColumnsTrigger++;
        }
        public void ReconcileOpenRowsWithCollection(IReadOnlyList<CollectionCard> collection)
        {
            var currentById = collection.ToDictionary(c => c.CardId);

            for (int i = CardsToAddOrEdit.Count - 1; i >= 0; i--)
            {
                var row = CardsToAddOrEdit[i];
                var card = row.CardToAddOrEdit;

                if (card.CardId is not int cardId)
                {
                    continue;
                }

                if (!currentById.TryGetValue(cardId, out var current))
                {
                    CardsToAddOrEdit.RemoveAt(i);
                    continue;
                }

                card.CardsOwned = current.CardsOwned;
                card.CardsForTrade = current.CardsForTrade;
                card.SellPrice = current.SellPrice;
                card.SellPriceCurrency = current.SellPriceCurrency;
                card.SelectedCondition = current.SelectedCondition;
                card.Language = current.Language;
                card.SelectedFinish = current.SelectedFinish;
                card.SelectedLocationId = current.SelectedLocationId;
                card.Comment = current.Comment;

                // Refresh the displayed value while preserving any uncommitted edit.
                var (Price, Currency) = GetSellPriceDisplay(card, _getSellPriceDisplayCurrency(), _getUsdToEurRate());
                row.SellPriceEditor.RefreshDisplay(Price, Currency);
            }

            RefreshColumnsTrigger++;
            OnPropertyChanged(nameof(IsCollectionEditVisible));
            OnPropertyChanged(nameof(ShowCounts));
        }
        public void RefreshSellPriceEditors()
        {
            var displayCurrency = _getSellPriceDisplayCurrency();
            var usdToEurRate = _getUsdToEurRate();

            foreach (var row in CardsToAddOrEdit)
            {
                var (Price, Currency) = GetSellPriceDisplay(row.CardToAddOrEdit, displayCurrency, usdToEurRate);

                row.SellPriceEditor.RefreshDisplay(Price, Currency);
            }
        }

        // Properties for binding to the listview
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
            await AddSelectedCardToListViewInternal(param, isEdit: false);
        }

        [RelayCommand]
        private async Task EditSelectedCards(object param)
        {
            await AddSelectedCardToListViewInternal(param, isEdit: true);
        }
        private async Task AddSelectedCardToListViewInternal(object param, bool isEdit)
        {
            if (param is not IEnumerable<object> selectedItems)
            {
                return;
            }

            StatusMessage = string.Empty;

            var existingEditCardIds = CardsToAddOrEdit.Select(r => r.CardToAddOrEdit.CardId).Where(id => id.HasValue).Select(id => id!.Value).ToHashSet();

            foreach (var selected in selectedItems)
            {
                if (!TryGetSelectedCardInfo(selected, out var printing, out var collectionCard))
                {
                    continue;
                }

                if (isEdit && collectionCard?.CardId is int cardId && existingEditCardIds.Contains(cardId))
                {
                    continue;
                }

                var editable = await _service.CreateCardForListAsync(printing, collectionCard, isEdit);

                if (editable is null)
                {
                    continue;
                }

                var (Price, Currency) = GetSellPriceDisplay(editable, _getSellPriceDisplayCurrency(), _getUsdToEurRate());

                CardsToAddOrEdit.Add(new CollectionCardDraftRowViewModel(editable, _availableLocations, RefreshColumnsCommand, CountCommittedCommand, initialDisplaySellPrice: Price, displayCurrency: Currency));
            }

            ClearSelectionTrigger++;
            RefreshColumnsTrigger++;
        }
        private static bool TryGetSelectedCardInfo(object selected, out PrintingCard printing, out CollectionCard? collectionCard)
        {
            switch (selected)
            {
                case PrintingCard selectedPrinting:
                    printing = selectedPrinting;
                    collectionCard = null;
                    return true;

                case CollectionCard selectedCollectionCard:
                    printing = selectedCollectionCard.Printing;
                    collectionCard = selectedCollectionCard;
                    return true;

                default:
                    printing = default!;
                    collectionCard = null;
                    return false;
            }
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
        private static void IncrementCount(CollectionCardDraftRowViewModel? row)
        {
            if (row is not null)
            {
                row.CardsOwned++;
            }
        }

        [RelayCommand]
        private void DecrementCount(CollectionCardDraftRowViewModel? row)
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
        private static void IncrementTrade(CollectionCardDraftRowViewModel? row)
        {
            if (row is not null && row.CardsForTrade < row.CardsOwned)
            {
                row.CardsForTrade++;
            }
        }

        [RelayCommand]
        private static void DecrementTrade(CollectionCardDraftRowViewModel? row)
        {
            if (row is not null && row.CardsForTrade > 0)
            {
                row.CardsForTrade--;
            }
        }

        [RelayCommand]
        private void CountCommitted(CollectionCardDraftRowViewModel? row)
        {
            if (row is null)
            {
                return;
            }

            if (row.CardsOwned < row.CardsForTrade)
            {
                row.CardsForTrade = row.CardsOwned;
            }

            if (_removeCardWhenZero && row.CardsOwned <= 0)
            {
                CardsToAddOrEdit.Remove(row);
            }

            RefreshColumnsTrigger++;
        }

        [RelayCommand]
        private void SplitOneRowOut(CollectionCardDraftRowViewModel? row)
        {
            if (row is null)
            {
                return;
            }

            SplitOneRowOutInternal(row);
        }

        [RelayCommand]
        private void SplitAllRowsOut(CollectionCardDraftRowViewModel? row)
        {
            if (row is null)
            {
                return;
            }

            while (row.CardsOwned > 1)
            {
                SplitOneRowOutInternal(row);
            }
        }
        private void SplitOneRowOutInternal(CollectionCardDraftRowViewModel row)
        {
            if (row.CardsOwned <= 1)
            {
                return;
            }

            if (row.CardsOwned == row.CardsForTrade)
            {
                row.CardsForTrade--;
            }

            row.CardsOwned--;

            var splitDraft = CollectionCardDraftFactory.FromSplit(row.CardToAddOrEdit);
            var (Price, Currency) = GetSellPriceDisplay(splitDraft, _getSellPriceDisplayCurrency(), _getUsdToEurRate());

            CardsToAddOrEdit.Add(new CollectionCardDraftRowViewModel(splitDraft, _availableLocations, RefreshColumnsCommand, CountCommittedCommand, initialDisplaySellPrice: Price, displayCurrency: Currency));

            RefreshColumnsTrigger++;
        }

        // Submit cards from listview
        [RelayCommand]
        private Task SubmitNewCardsAsync()
        {
            return SubmitStagedCardsAsync("Added the following cards to your collection:");
        }

        [RelayCommand]
        private Task SubmitCardEditsAsync()
        {
            return SubmitStagedCardsAsync("Updated the following cards with these values:");
        }
        private async Task SubmitStagedCardsAsync(string summaryTitle)
        {
            // Commit price edits before submitting the staged rows.
            if (!CommitSellPriceEditors())
            {
                return;
            }

            await SubmitBatchAsync(
                CardsToAddOrEdit.Select(row => row.CardToAddOrEdit),
                (cards, snapshot) => _service.SubmitCardBatchAsync(cards, snapshot),
                clearAfter: true,
                summaryTitle: summaryTitle);
        }

        [RelayCommand]
        private async Task SubmitNewCardsWithDefaults(object? param)
        {
            if (param is not IEnumerable<object> selectedItems)
            {
                return;
            }

            var printings = selectedItems.OfType<PrintingCard>().ToList();

            if (printings.Count == 0)
            {
                return;
            }

            var snapshot = _cardCollectionHost.CreateCollectionIdentitySnapshot();
            var changeSet = await _service.SubmitNewCardsWithDefaultsBatchAsync(printings, snapshot);

            CollectionChanged?.Invoke(this, changeSet);
            StatusMessage = BuildSubmitSummary(changeSet, [.. printings.Select(CollectionCardDraftFactory.FromPrintingCard)], "Added the following cards with default values:");
        }

        [RelayCommand]
        private async Task DeleteSelectedCardsAsync(object? param)
        {
            if (param is not IEnumerable<object> selectedItems)
            {
                return;
            }

            var toDelete = selectedItems.OfType<CollectionCard>().Select(CollectionCardDraftFactory.FromCollectionCardForDelete).ToList();

            if (toDelete.Count == 0)
            {
                return;
            }

            await SubmitBatchAsync(toDelete, (cards, snapshot) => _service.SubmitCardBatchAsync(cards, snapshot), clearAfter: true, summaryTitle: "Deleted the following cards from your collection:");
        }

        [RelayCommand]
        private async Task PutAllForTradeAsync(object? param)
        {
            if (param is not IEnumerable<object> selectedItems)
            {
                return;
            }

            var drafts = selectedItems.OfType<CollectionCard>().Select(CollectionCardDraftFactory.FromCollectionCardForTradeAll).ToList();

            if (drafts.Count == 0)
            {
                return;
            }

            await SubmitBatchAsync(drafts, (cards, snapshot) => _service.SubmitCardBatchAsync(cards, snapshot), clearAfter: false, summaryTitle: "Put the following cards up for trade:");
        }

        [RelayCommand]
        private async Task SetNoneForTradeAsync(object? param)
        {
            if (param is not IEnumerable<object> selectedItems)
            {
                return;
            }

            var drafts = selectedItems.OfType<CollectionCard>().Select(CollectionCardDraftFactory.FromCollectionCardForTradeNone).ToList();

            if (drafts.Count == 0)
            {
                return;
            }

            await SubmitBatchAsync(CardsToAddOrEdit.Select(r => r.CardToAddOrEdit), (cards, snapshot) => _service.SubmitCardBatchAsync(cards, snapshot), clearAfter: false, summaryTitle: "Set the following cards not for trade:");
        }

        [RelayCommand]
        private async Task SetLocationForSelectedCardsAsync(SetLocationForSelectedCardsParameter? parameter)
        {
            if (parameter is null)
            {
                return;
            }

            var edits = parameter.SelectedItems.OfType<CollectionCard>().Select(card => CollectionCardDraftFactory.FromCollectionCardWithLocation(card, parameter.LocationId)).ToList();

            if (edits.Count == 0)
            {
                return;
            }

            await SubmitBatchAsync(edits, (cards, snapshot) => _service.SubmitCardBatchAsync(cards, snapshot), clearAfter: false,
                summaryTitle: parameter.LocationId is null
                    ? "Removed location from the following cards:"
                    : "Updated location for the following cards:");
        }

        [RelayCommand]
        private Task MatchRetailPriceForSelectedCardsAsync(MatchRetailPriceForSelectedCardsParameter? parameter)
        {
            return SubmitRetailPriceMatchAsync(parameter, discountRate: 0m, summaryTitle: "Matched sell price to retailer price for the following cards:");
        }

        [RelayCommand]
        private Task MatchRetailPriceMinusTenPercentForSelectedCardsAsync(MatchRetailPriceForSelectedCardsParameter? parameter)
        {
            return SubmitRetailPriceMatchAsync(parameter, discountRate: 0.10m, summaryTitle: "Matched sell price to retailer price minus 10% for the following cards:");
        }
        private async Task SubmitRetailPriceMatchAsync(MatchRetailPriceForSelectedCardsParameter? parameter, decimal discountRate, string summaryTitle)
        {
            if (parameter is null)
            {
                return;
            }

            var edits = parameter.SelectedItems
                .OfType<CollectionCard>()
                .Select(card => CollectionCardDraftFactory.FromCollectionCardWithRetailPriceDiscount(card, discountRate, parameter.Currency))
                .OfType<CollectionCardDraft>()
                .ToList();

            if (edits.Count == 0)
            {
                return;
            }

            await SubmitBatchAsync(edits, (cards, snapshot) => _service.SubmitCardBatchAsync(cards, snapshot), clearAfter: false, summaryTitle);
        }
        // Shared helpers 
        private async Task SubmitBatchAsync(IEnumerable<CollectionCardDraft> cards, Func<IEnumerable<CollectionCardDraft>, ICollectionIdentitySnapshot, Task<CollectionChangeSet<CollectionCardDbRow>>> submit, bool clearAfter, string summaryTitle)
        {
            var cardList = cards.ToList();
            var snapshot = _cardCollectionHost.CreateCollectionIdentitySnapshot();
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
        private static string BuildSubmitSummary(CollectionChangeSet<CollectionCardDbRow> changeSet, IReadOnlyList<CollectionCardDraft> submittedCards, string summaryTitle)
        {
            var nameByUuid = submittedCards
                .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                .GroupBy(c => c.Uuid, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Name!, StringComparer.OrdinalIgnoreCase);

            var locationByUuid = submittedCards
                .Where(c => c.SelectedLocationId.HasValue)
                .GroupBy(c => c.Uuid, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().SelectedLocationDisplayName, StringComparer.OrdinalIgnoreCase);

            var sb = new StringBuilder();

            if (changeSet.AddedOrUpdated.Count > 0)
            {
                sb.AppendLine(summaryTitle).AppendLine();

                foreach (var row in changeSet.AddedOrUpdated)
                {
                    var identity = row.Identity;
                    var name = nameByUuid.TryGetValue(identity.Uuid, out var cardName)
                        ? cardName
                        : identity.Uuid;
                    var locationDisplayName = locationByUuid.TryGetValue(identity.Uuid, out var locationName)
                        ? locationName
                        : "No location";

                    sb.AppendLine(
                        $"- {name} " +
                        $"(Condition: {identity.Condition}, " +
                        $"Language: {identity.Language}, " +
                        $"Finish: {identity.Finish}, " +
                        $"Location: {locationDisplayName}, " +
                        $"Owned: {row.CardsOwned}, " +
                        $"Trade: {row.CardsForTrade}), " +
                        $"Sell Price: {row.SellPrice} {row.SellPriceCurrency}");
                }
            }

            if (changeSet.RemovedIds.Count > 0)
            {
                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }

                sb.AppendLine("Deleted the following cards from your collection:").AppendLine();

                foreach (var c in submittedCards.Where(c => c.CardId.HasValue && changeSet.RemovedIds.Contains(c.CardId.Value)))
                {
                    sb.AppendLine(
                        $"- {c.Name ?? c.Uuid} " +
                        $"(Condition: {c.SelectedCondition}, " +
                        $"Language: {c.Language}, " +
                        $"Finish: {c.SelectedFinish})");
                }
            }

            return sb.ToString().TrimEnd();
        }
        private static IReadOnlyList<LocationMenuItemViewModel> BuildLocationMenuItems(IReadOnlyList<CardLocation> locations)
        {
            var decks =
                locations
                    .Where(x => x.Type == CardLocationType.Deck)
                    .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(LocationMenuItemViewModel.FromLocation)
                    .ToList();

            var storage =
                locations
                    .Where(x => x.Type == CardLocationType.Storage)
                    .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(LocationMenuItemViewModel.FromLocation)
                    .ToList();

            return
            [
                new LocationMenuItemViewModel{ Header = "No location", LocationId = null},
                new LocationMenuItemViewModel{ Header = "Decks", Children = decks},
                new LocationMenuItemViewModel{ Header = "Storage", Children = storage}
            ];
        }
        private static (decimal? Price, string Currency) GetSellPriceDisplay(CollectionCardDraft draft, string displayCurrency, decimal? usdToEurRate)
        {
            ArgumentNullException.ThrowIfNull(draft);

            var targetCurrency = displayCurrency.ToUpperInvariant();

            if (targetCurrency is not ("USD" or "EUR"))
            {
                throw new ArgumentException($"Unsupported display currency '{displayCurrency}'.", nameof(displayCurrency));
            }

            if (draft.SellPrice is not decimal sellPrice)
            {
                return (null, targetCurrency);
            }

            if (string.IsNullOrWhiteSpace(draft.SellPriceCurrency))
            {
                throw new InvalidOperationException("A draft with a sell price must also have a sell price currency.");
            }

            var sourceCurrency = draft.SellPriceCurrency.ToUpperInvariant();

            if (sourceCurrency == targetCurrency)
            {
                return (sellPrice, targetCurrency);
            }

            if (usdToEurRate is not decimal rate)
            {
                throw new InvalidOperationException("An exchange rate is required to display this sell price in another currency.");
            }

            var convertedPrice = CurrencyConversionLogic.Convert(sellPrice, sourceCurrency, targetCurrency, rate);

            return (convertedPrice, targetCurrency);
        }
        private bool CommitSellPriceEditors()
        {
            var allValid = true;

            foreach (var row in CardsToAddOrEdit)
            {
                if (!row.SellPriceEditor.TryCommit())
                {
                    allValid = false;
                }
            }

            return allValid;
        }
    }
}
