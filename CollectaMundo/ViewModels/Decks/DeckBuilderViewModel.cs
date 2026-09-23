using CollectaMundo.ApplicationServices.Decks;
using CollectaMundo.ApplicationServices.Decks.Models;
using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.DomainLogic.Decks.Models.BuildingRules;
using CollectaMundo.DomainLogic.Decks.Models.Enums;
using CollectaMundo.DomainLogic.Decks.Models.Stats;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.DomainLogic.Shared.CollectionSnapshot;
using CollectaMundo.ViewModels.CardLists;
using CollectaMundo.ViewModels.Decks.Models;
using CollectaMundo.ViewModels.Decks.Models.DragMoveViewRequests;
using CollectaMundo.ViewModels.Decks.Models.RowViewModels;
using CollectaMundo.ViewModels.Filtering;
using CollectaMundo.ViewModels.Shell;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace CollectaMundo.ViewModels.Decks
{
    public partial class DeckBuilderViewModel(IDeckBuilderService deckBuilderService, CardListViewModel<OracleCard> oracleCardsVM, FilterPanelViewModel filterPanelViewModel, ICardCollectionHost cardCollectionHost) : ObservableObject
    {
        #region Dependencies and private state
        private readonly IDeckBuilderService _deckBuilderService = deckBuilderService;
        private readonly CardListViewModel<OracleCard> _oracleCardsVM = oracleCardsVM;
        private readonly FilterPanelViewModel _filterPanelViewModel = filterPanelViewModel;
        private readonly ICardCollectionHost _cardCollectionHost = cardCollectionHost;
        private ICollectionQuantitySnapshot? _collectionQuantitySnapshot;
        private readonly IReadOnlyList<DeckZoneViewModel> _zones =
        [
            new() { Section = DeckSection.Mainboard, DisplayName = "Deck" },
            new() { Section = DeckSection.Sideboard, DisplayName = "Sideboard" },
            new() { Section = DeckSection.Maybeboard, DisplayName = "Maybeboard" },
            new() { Section = DeckSection.Commander, DisplayName = "Command zone" },
            new() { Section = DeckSection.Companion, DisplayName = "Companion zone" }
        ];

        #endregion

        #region Public state

        // Child ViewModels / filters
        public CardListViewModel<OracleCard> CardsVM => _oracleCardsVM;
        public FilterPanelViewModel FilterVM => _filterPanelViewModel;
        public FilterItemViewModel? NameFilter => FilterVM.Filters.TryGetValue("Name", out var filter) ? filter : null;

        // Deck identity
        public int? DeckLocationId { get; internal set; }
        public string DeckName { get; private set; } = string.Empty;
        public string? DeckFormat { get; internal set; }
        public string DeckFormatDisplayName { get; private set; } = string.Empty;

        // Deck zones
        public DeckZoneViewModel MainboardZone => GetZone(DeckSection.Mainboard);
        public DeckZoneViewModel SideboardZone => GetZone(DeckSection.Sideboard);
        public DeckZoneViewModel MaybeboardZone => GetZone(DeckSection.Maybeboard);
        public DeckZoneViewModel CommanderZone => GetZone(DeckSection.Commander);
        public DeckZoneViewModel CompanionZone => GetZone(DeckSection.Companion);

        // Selection state
        public ObservableCollection<OracleCard> SelectedOracleCards { get; } = [];
        public ObservableCollection<DeckBoxCardViewModel> SelectedDeckBoxCards { get; } = [];
        public IList SelectedAddItems => SelectedDeckBoxCard is not null
            ? SelectedDeckBoxCards
            : SelectedOracleCards;
        public OracleCard? SelectedAddCard => SelectedDeckBoxCard?.OracleCard ?? SelectedOracleCard;

        [ObservableProperty]
        private DeckCardEntryViewModel? selectedDeckCard;

        [ObservableProperty]
        private OracleCard? selectedOracleCard;

        [ObservableProperty]
        private DeckBoxCardViewModel? selectedDeckBoxCard;

        // Presentation state

        [ObservableProperty]
        private bool isAddButtonVisible;

        [ObservableProperty]
        private bool canSetSelectedCardAsCommander;

        [ObservableProperty]
        private bool canSetSelectedCardAsCompanion;

        [ObservableProperty]
        private bool isSideboardZoneVisible;

        [ObservableProperty]
        private bool isCommanderZoneVisible;

        [ObservableProperty]
        private bool isCompanionZoneVisible;

        [ObservableProperty]
        private bool isDeckBoxDataGridVisible;

        [ObservableProperty]
        private int refreshColumnsTrigger;

        [ObservableProperty]
        private IReadOnlyList<DeckBoxCardViewModel> deckBoxCards = [];

        [ObservableProperty]
        private DeckStats stats = new();

        #endregion

        #region Events and property callbacks

        // Events

        public event EventHandler? ExitEditorRequested;
        public event EventHandler<OracleCardImageSelectionRequest?>? CardImageSelectionRequested;

        // Observable property callbacks
        partial void OnSelectedDeckCardChanged(DeckCardEntryViewModel? value)
        {
            ShowCardImage(value?.OracleId, value?.CardName);
        }
        partial void OnSelectedOracleCardChanged(OracleCard? value)
        {
            if (value is null && SelectedDeckBoxCard is not null)
            {
                return;
            }

            if (value is not null)
            {
                SelectedDeckBoxCard = null;
            }

            OnPropertyChanged(nameof(SelectedAddCard));
            OnPropertyChanged(nameof(SelectedAddItems));

            RefreshRuleDependentProperties();
            ShowCardImage(value?.ScryfallOracleId, value?.Name);
        }
        partial void OnSelectedDeckBoxCardChanged(DeckBoxCardViewModel? value)
        {
            if (value is null && SelectedOracleCard is not null)
            {
                return;
            }

            if (value is not null)
            {
                SelectedOracleCard = null;
            }

            OnPropertyChanged(nameof(SelectedAddCard));
            OnPropertyChanged(nameof(SelectedAddItems));

            RefreshRuleDependentProperties();
            ShowCardImage(value?.OracleId, value?.CardName);
        }

        #endregion

        #region Lifecycle
        public async Task BeginEditAsync(DeckManagementRecord deck, DeckFormatOption? formatOption)
        {
            var entries = await _deckBuilderService.LoadDeckAsync(deck.LocationId);

            DeckLocationId = deck.LocationId;
            DeckName = deck.Name;

            DeckFormat = deck.Format;
            DeckFormatDisplayName = formatOption?.DisplayName ?? deck.Format ?? string.Empty;

            _collectionQuantitySnapshot = _cardCollectionHost.CreateCollectionQuantitySnapshot();

            LoadDeckBoxCards();

            var deckCards = new List<DeckCardState>();

            foreach (var entry in entries)
            {
                var oracleCard = CardsVM.Cards.FirstOrDefault(card => string.Equals(card.ScryfallOracleId, entry.OracleId, StringComparison.OrdinalIgnoreCase));

                if (oracleCard is null)
                {
                    continue;
                }

                deckCards.Add(new DeckCardState
                {
                    Card = oracleCard,
                    DesiredQuantity = entry.DesiredQuantity,
                    Section = entry.Section
                });
            }

            ClearZones();
            AddDeckRows(deckCards);
            RefreshAll();
        }
        private void LoadDeckBoxCards()
        {
            if (DeckLocationId is not int locationId || _collectionQuantitySnapshot is null)
            {
                DeckBoxCards = [];
                IsDeckBoxDataGridVisible = false;
                return;
            }

            DeckBoxCards = [.. CardsVM.Cards.Select(card => new
                {
                    Card = card,
                    AllocatedQuantity = _collectionQuantitySnapshot.GetAllocatedQuantity(card.ScryfallOracleId,locationId)
                }).Where(x => x.AllocatedQuantity > 0).Select(x => new DeckBoxCardViewModel
                {
                    OracleCard = x.Card,
                    AllocatedQuantity = x.AllocatedQuantity
                })
                .OrderBy(row => CardSort.GetTypeRank(row.OracleCard.Types, row.OracleCard.GamePlayCard))
                .ThenBy(row => CardSort.GetColorRank(row.OracleCard.Colors))
                .ThenBy(row => row.ManaValue ?? 0)
                .ThenBy(row => row.CardName, StringComparer.OrdinalIgnoreCase)
            ];

            IsDeckBoxDataGridVisible = DeckBoxCards.Count > 0;
        }

        #endregion

        #region Commands

        // Navigation back to deck management
        [RelayCommand]
        private void BackToDeckManagement()
        {
            ClearSelections();
            ExitEditorRequested?.Invoke(this, EventArgs.Empty);
        }

        // Clear selections 

        [RelayCommand]
        private void ActivateDeckSelection()
        {
            SelectedOracleCard = null;
            SelectedDeckBoxCard = null;

            if (SelectedDeckCard is not null)
            {
                ShowCardImage(SelectedDeckCard.OracleId, SelectedDeckCard.CardName);
            }
        }

        [RelayCommand]
        protected void ClearSelections()
        {
            SelectedOracleCard = null;
            SelectedDeckCard = null;
            SelectedDeckBoxCard = null;
        }

        // Adding a card
        [RelayCommand]
        private Task AddCardToDeckAsync(object? parameter)
        {
            return AddCardsToDeckZoneAsync(parameter, 1, DeckSection.Mainboard);
        }

        [RelayCommand]
        private Task AddPlaySetToDeckAsync(object? parameter)
        {
            return AddCardsToDeckZoneAsync(parameter, 4, DeckSection.Mainboard);
        }

        [RelayCommand]
        private Task AddCardToSideboardAsync(object? parameter)
        {
            return AddCardsToDeckZoneAsync(parameter, 1, DeckSection.Sideboard);
        }

        [RelayCommand]
        private Task AddCardToMaybeboardAsync(object? parameter)
        {
            return AddCardsToDeckZoneAsync(parameter, 1, DeckSection.Maybeboard);
        }

        [RelayCommand]
        private async Task SetCardAsCommanderAsync()
        {
            if (SelectedAddCard is null || DeckLocationId is null)
            {
                return;
            }

            var result = await _deckBuilderService.SetCommanderAsync(DeckLocationId.Value, DeckFormat, CreateDeckCardStates(), SelectedAddCard);

            ApplySuccessfulMutation(result);
        }

        [RelayCommand]
        private async Task SetCardAsCompanionAsync()
        {

            if (SelectedAddCard is null || DeckLocationId is null)
            {
                return;
            }

            var result = await _deckBuilderService.SetCompanionAsync(DeckLocationId.Value, DeckFormat, CreateDeckCardStates(), SelectedAddCard);

            ApplySuccessfulMutation(result);
        }

        [RelayCommand]
        private Task AddDraggedOracleCardAsync(DeckOracleCardDropRequest? request)
        {
            if (request is null)
            {
                return Task.CompletedTask;
            }

            return AddCardsToDeckZoneAsync(request.Cards, request.Quantity, request.DestinationSection);
        }

        // Add Card helpers
        private async Task AddCardsToDeckZoneAsync(object? parameter, int quantity, DeckSection section)
        {
            if (DeckLocationId is null)
            {
                return;
            }

            var cards = GetOracleCardsFromCommandParameter(parameter).ToList();

            if (cards.Count == 0)
            {
                return;
            }

            var result = await _deckBuilderService.AddCardsAsync(DeckLocationId.Value, CreateDeckCardStates(), cards, quantity, section);

            ApplySuccessfulMutation(result);
        }
        private static IEnumerable<OracleCard> GetOracleCardsFromCommandParameter(object? parameter)
        {
            if (parameter is not IEnumerable items)
            {
                yield break;
            }

            foreach (var item in items)
            {
                switch (item)
                {
                    case OracleCard card:
                        yield return card;
                        break;

                    case DeckBoxCardViewModel row:
                        yield return row.OracleCard;
                        break;
                }
            }
        }

        // Moving cards to sideboard from right-click context menu
        [RelayCommand]
        private Task MoveOneToSideboardAsync(object? parameter)
        {
            var moves = GetDeckRowsFromCommandParameter(parameter).Where(row => row.Section != DeckSection.Sideboard).Select(row => new DeckCardMoveRequest(row.OracleCard, row.Section, 1)).ToList();
            return MoveCardsAsync(moves, DeckSection.Sideboard);
        }

        [RelayCommand]
        private Task MoveAllToSideboardAsync(object? parameter)
        {
            var moves = GetDeckRowsFromCommandParameter(parameter).Where(row => row.Section != DeckSection.Sideboard).Select(row => new DeckCardMoveRequest(row.OracleCard, row.Section, row.DesiredQuantity)).ToList();
            return MoveCardsAsync(moves, DeckSection.Sideboard);
        }

        // Drag-and-drop card movement
        [RelayCommand]
        private async Task HandleDeckCardDragAsync(DeckCardDragRequest? request)
        {
            if (request is null || DeckLocationId is null || request.Items.Count == 0)
            {
                return;
            }

            if (request.DestinationSection is DeckSection destinationSection)
            {
                var moves = request.Items.Select(item => new DeckCardMoveRequest(item.Card.OracleCard, item.Card.Section, item.Quantity)).ToList();
                await MoveCardsAsync(moves, destinationSection);
                return;
            }

            await RemoveDraggedCardQuantitiesAsync(request.Items);
        }
        private async Task RemoveDraggedCardQuantitiesAsync(IReadOnlyList<DeckCardDragItem> items)
        {
            var removals = items.Select(item => new DeckCardQuantityRemoval(item.Card.OracleCard, item.Card.Section, item.Quantity)).ToList();
            var result = await _deckBuilderService.RemoveCardQuantitiesAsync(DeckLocationId!.Value, CreateDeckCardStates(), removals);

            ApplySuccessfulMutation(result);
        }

        // Deleting a card
        [RelayCommand]
        private async Task DeleteDeckCardsAsync(object? param)
        {
            if (DeckLocationId is null)
            {
                return;
            }

            var rows = GetDeckRowsFromCommandParameter(param).ToList();

            if (rows.Count == 0)
            {
                return;
            }

            var result = await _deckBuilderService.DeleteCardsAsync(DeckLocationId.Value, CreateDeckCardStates(), [.. rows.Select(row => new DeckCardIdentityRecord(row.OracleId, row.Section))]);
            ApplySuccessfulMutation(result);
        }
        private static IEnumerable<DeckCardEntryViewModel> GetDeckRowsFromCommandParameter(object? param)
        {
            if (param is DeckCardEntryViewModel singleRow)
            {
                yield return singleRow;
                yield break;
            }

            if (param is IEnumerable selectedItems)
            {
                foreach (var item in selectedItems)
                {
                    if (item is DeckCardEntryViewModel row)
                    {
                        yield return row;
                    }
                }
            }
        }

        // Incrementing and decrementing card quantity
        [RelayCommand]
        private Task IncrementDeckCardQuantityAsync(DeckCardEntryViewModel? row)
        {
            if (row is null)
            {
                return Task.CompletedTask;
            }

            return SetCardQuantityAsync(row, row.DesiredQuantity + 1);
        }

        [RelayCommand]
        private Task DecrementDeckCardQuantityAsync(DeckCardEntryViewModel? row)
        {
            if (row is null)
            {
                return Task.CompletedTask;
            }

            return SetCardQuantityAsync(row, row.DesiredQuantity - 1);
        }
        private async Task SetCardQuantityAsync(DeckCardEntryViewModel row, int desiredQuantity)
        {
            if (DeckLocationId is null)
            {
                return;
            }

            var result = await _deckBuilderService.SetCardQuantityAsync(DeckLocationId.Value, CreateDeckCardStates(), new DeckCardIdentityRecord(row.OracleId, row.Section), desiredQuantity);

            if (!result.Succeeded)
            {
                Debug.WriteLine($"Failed to change deck card quantity: {result.Message}");
                return;
            }

            var updatedCard = result.Cards.FirstOrDefault(card => card.Section == row.Section && string.Equals(card.Card.ScryfallOracleId, row.OracleId, StringComparison.OrdinalIgnoreCase));

            if (updatedCard is null)
            {
                GetZone(row.Section).Cards.Remove(row);
                RefreshAll();
                return;
            }

            if (row.DesiredQuantity != updatedCard.DesiredQuantity)
            {
                row.DesiredQuantity = updatedCard.DesiredQuantity;
            }
        }

        #endregion

        #region Refresh and Helpers

        // Refresh methods
        private void RefreshAll()
        {
            RefreshZoneVisibility();
            RefreshRuleDependentProperties();
            RefreshOwnedQuantityStatus();
            RefreshColumns();
            RefreshStats();
        }
        private void RefreshAfterQuantityChanged()
        {
            RefreshOwnedQuantityStatus();
            RefreshColumns();
            RefreshStats();
        }
        private void RefreshZoneVisibility()
        {
            IsSideboardZoneVisible = SideboardZone.Cards.Count > 0;
            IsCommanderZoneVisible = CommanderZone.Cards.Count > 0 && CommanderFormats.IsCommanderLike(DeckFormat);
            IsCompanionZoneVisible = CompanionZone.Cards.Count > 0;
        }
        private void RefreshRuleDependentProperties()
        {
            var selectedCard = SelectedAddCard;

            var availability = selectedCard is null
                ? new DeckActionAvailability()
                : _deckBuilderService.GetActionAvailability(DeckFormat, CreateDeckCardStates(), selectedCard);

            IsAddButtonVisible = selectedCard is not null;

            CanSetSelectedCardAsCommander = availability.CanSetAsCommander && IsAddButtonVisible;
            CanSetSelectedCardAsCompanion = availability.CanSetAsCompanion && IsAddButtonVisible;
        }
        private void RefreshOwnedQuantityStatus()
        {
            var trackedRows = AllDeckCards.Where(row => row.Section != DeckSection.Maybeboard).ToList();
            var requiredByOracleId = trackedRows.GroupBy(row => row.OracleId, StringComparer.OrdinalIgnoreCase).ToDictionary(
                group => group.Key,
                group => group.Sum(row => row.DesiredQuantity),
                StringComparer.OrdinalIgnoreCase);

            foreach (var row in trackedRows)
            {
                var requiredQuantity = requiredByOracleId.GetValueOrDefault(row.OracleId);

                if (CollectionQuantityRules.RequiresAvailabilityCheck(row.OracleCard))
                {
                    row.HasInsufficientAvailableQuantity = row.AvailableQuantity < requiredQuantity;
                }
            }
        }
        private void RefreshColumns()
        {
            RefreshColumnsTrigger++;
        }
        private void RefreshStats()
        {
            Stats = _deckBuilderService.CalculateDeckStats(CreateDeckCardStates());
        }

        // Mutation orchestration
        private async Task MoveCardsAsync(IReadOnlyList<DeckCardMoveRequest> moves, DeckSection destinationSection)
        {
            if (DeckLocationId is null || moves.Count == 0)
            {
                return;
            }

            var result = await _deckBuilderService.MoveCardsAsync(DeckLocationId.Value, CreateDeckCardStates(), moves, destinationSection);

            ApplySuccessfulMutation(result);
        }
        private void ApplySuccessfulMutation(DeckMutationResult result)
        {
            if (!result.Succeeded)
            {
                Debug.WriteLine($"Deck mutation failed: {result.Message}");
                return;
            }

            ClearZones();

            AddDeckRows(result.Cards);

            RefreshAll();
        }

        // Deck-state projection
        private IReadOnlyList<DeckCardState> CreateDeckCardStates()
        {
            return [.. AllDeckCards.Select(x => new DeckCardState
            {
                Card = x.OracleCard,
                DesiredQuantity = x.DesiredQuantity,
                Section = x.Section
            })];
        }

        // Deck row / zone population
        private void ClearZones()
        {
            foreach (var zone in _zones)
            {
                zone.Cards.Clear();
            }
        }
        private void AddDeckRows(IReadOnlyCollection<DeckCardState> deckCards)
        {
            var rows = deckCards.Select(card => CreateDeckRow(card, deckCards)).ToList();

            foreach (var row in rows.Where(row => row.Section is DeckSection.Mainboard or DeckSection.Sideboard or DeckSection.Maybeboard)
                .OrderBy(row => CardSort.GetTypeRank(row.OracleCard.Types, row.OracleCard.GamePlayCard))
                .ThenBy(row => CardSort.GetColorRank(row.OracleCard.Colors))
                .ThenBy(row => row.ManaValue ?? 0)
                .ThenBy(row => row.CardName, StringComparer.OrdinalIgnoreCase))
            {
                AddRowToZone(row);
            }

            // Commander/Companion aren't part of normal deck sorting.
            foreach (var row in rows.Where(row => row.Section is not (DeckSection.Mainboard or DeckSection.Sideboard or DeckSection.Maybeboard)))
            {
                AddRowToZone(row);
            }
        }
        private DeckCardEntryViewModel CreateDeckRow(DeckCardState card, IReadOnlyCollection<DeckCardState> deckCards)
        {
            var entry = new DeckCardEntry
            {
                DeckLocationId = DeckLocationId ?? 0,
                OracleId = card.Card.ScryfallOracleId,
                CardName = card.Card.Name,
                DesiredQuantity = card.DesiredQuantity,
                Section = card.Section
            };

            var validation = new DeckCardValidationResult { IsLegal = true };

            // Only validate if the deck format is not null and not casual
            if (DeckFormat != null && DeckFormat != "casual")
            {
                validation = _deckBuilderService.ValidateCard(DeckFormat, deckCards, entry, card.Card);
            }

            var oracleId = card.Card.ScryfallOracleId;

            var ownedQuantity = _collectionQuantitySnapshot?.GetOwnedQuantity(oracleId) ?? 0;

            var allocatedQuantity = DeckLocationId is int locationId ? _collectionQuantitySnapshot?.GetAllocatedQuantity(oracleId, locationId) ?? 0 : 0;

            var availableQuantity = DeckLocationId is int currentLocationId
                ? _collectionQuantitySnapshot?.GetAvailableQuantity(oracleId, currentLocationId) ?? 0
                : ownedQuantity;

            return new DeckCardEntryViewModel(quantityCommitAsync: OnDeckCardQuantityCommitAsync, initialDesiredQuantity: card.DesiredQuantity, desiredQuantityChanged: _ => RefreshAfterQuantityChanged())
            {
                OracleCard = card.Card,
                Section = card.Section,
                IsLegal = validation.IsLegal,
                OwnedQuantity = ownedQuantity,
                AllocatedQuantity = allocatedQuantity,
                AvailableQuantity = availableQuantity
            };
        }
        private void AddRowToZone(DeckCardEntryViewModel row) { GetZone(row.Section).Cards.Add(row); }
        private DeckZoneViewModel GetZone(DeckSection section) { return _zones.First(z => z.Section == section); }

        // Quantity editing
        private Task OnDeckCardQuantityCommitAsync(DeckCardEntryViewModel? row)
        {
            if (row is null)
            {
                return Task.CompletedTask;
            }

            return SetCardQuantityAsync(row, row.DesiredQuantity);
        }

        // Derived deck collections
        private IEnumerable<DeckCardEntryViewModel> AllDeckCards => _zones.SelectMany(z => z.Cards);

        // Card image display
        private void ShowCardImage(string? oracleId, string? name)
        {
            var request = string.IsNullOrWhiteSpace(oracleId)
                ? new OracleCardImageSelectionRequest()
                : new OracleCardImageSelectionRequest(OracleId: oracleId, Name: name);

            CardImageSelectionRequested?.Invoke(this, request);
        }

        #endregion

    }
}
