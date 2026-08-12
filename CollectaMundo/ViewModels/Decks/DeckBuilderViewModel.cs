using CollectaMundo.ApplicationServices.Decks;
using CollectaMundo.ApplicationServices.Decks.Models;
using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.DomainLogic.Decks.Models.Enums;
using CollectaMundo.DomainLogic.Decks.Models.Records;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.DomainLogic.Shared.CollectionSnapshot;
using CollectaMundo.ViewModels.CardLists;
using CollectaMundo.ViewModels.Decks.Models;
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
        private readonly IDeckBuilderService _deckBuilderService = deckBuilderService;
        private readonly CardListViewModel<OracleCard> _oracleCardsVM = oracleCardsVM;
        private readonly FilterPanelViewModel _filterPanelViewModel = filterPanelViewModel;
        private readonly ICardCollectionHost _cardCollectionHost = cardCollectionHost;
        private ICollectionQuantitySnapshot? _collectionQuantitySnapshot;
        private IEnumerable<DeckCardEntryViewModel> AllDeckCards => Zones.SelectMany(z => z.Cards);

        // Filtered OracleCard list view model
        public CardListViewModel<OracleCard> CardsVM => _oracleCardsVM;

        // Filter panel view model
        public FilterPanelViewModel FilterVM => _filterPanelViewModel;

        // Bindable pass-through properties for the filters 
        public FilterItemViewModel? NameFilter => FilterVM.Filters.TryGetValue("Name", out var f) ? f : null;

        // Events for external notifications
        public event EventHandler? ExitEditorRequested;

        public event EventHandler<OracleCardImageSelectionRequest?>? CardImageSelectionRequested;

        // DeckZoneViewModels for each deck section
        public DeckZoneViewModel MainboardZone => GetZone(DeckSection.Mainboard);
        public DeckZoneViewModel SideboardZone => GetZone(DeckSection.Sideboard);
        public DeckZoneViewModel MaybeboardZone => GetZone(DeckSection.Maybeboard);
        public DeckZoneViewModel CommanderZone => GetZone(DeckSection.Commander);
        public DeckZoneViewModel CompanionZone => GetZone(DeckSection.Companion);
        private ObservableCollection<DeckZoneViewModel> Zones { get; } =
        [
            new() { Section = DeckSection.Mainboard, DisplayName = "Deck" },
            new() { Section = DeckSection.Sideboard, DisplayName = "Sideboard" },
            new() { Section = DeckSection.Maybeboard, DisplayName = "Maybeboard" },
            new() { Section = DeckSection.Commander, DisplayName = "Command zone" },
            new() { Section = DeckSection.Companion, DisplayName = "Companion zone" }
        ];
        private DeckZoneViewModel GetZone(DeckSection section) { return Zones.First(z => z.Section == section); }

        // Begin editing a deck - initializes the view model with the deck's current state
        public async Task BeginEditAsync(DeckManagementRecord deck)
        {
            var entries = await _deckBuilderService.LoadDeckAsync(deck.LocationId);

            DeckLocationId = deck.LocationId;
            DeckName = deck.Name;
            DeckFormat = deck.Format;

            _collectionQuantitySnapshot = _cardCollectionHost.CreateCollectionQuantitySnapshot();

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

        #region Observable Properties

        // Visibility rules
        [ObservableProperty]
        private bool isAddButtonVisible;

        [ObservableProperty]
        private bool canSetSelectedOracleCardAsCommander;

        [ObservableProperty]
        private bool canSetSelectedOracleCardAsCompanion;

        [ObservableProperty]
        private bool isCommanderZoneVisible;

        [ObservableProperty]
        private bool isSideboardZoneVisible;

        [ObservableProperty]
        private bool isMaybeboardZoneVisible;

        [ObservableProperty]
        private bool isCompanionZoneVisible;


        // Deck identity properties
        [ObservableProperty]
        private int? deckLocationId;

        [ObservableProperty]
        private string deckName = string.Empty;

        [ObservableProperty]
        private string? deckFormat;

        // Selected card properties

        // DeckCards datagrids
        [ObservableProperty]
        private DeckCardEntryViewModel? selectedDeckCard;
        partial void OnSelectedDeckCardChanged(DeckCardEntryViewModel? value)
        {
            ShowCardImage(value?.OracleId, value?.CardName);
        }

        // OracleCard datagrid
        [ObservableProperty]
        private OracleCard? selectedOracleCard;
        partial void OnSelectedOracleCardChanged(OracleCard? value)
        {
            RefreshRuleDependentProperties();
            ShowCardImage(value?.ScryfallOracleId, value?.Name);
        }
        private void ShowCardImage(string? oracleId, string? name)
        {
            var request = string.IsNullOrWhiteSpace(oracleId)
                ? new OracleCardImageSelectionRequest()
                : new OracleCardImageSelectionRequest(OracleId: oracleId, Name: name);

            CardImageSelectionRequested?.Invoke(this, request);
        }

        #endregion

        #region Commands
        // Navigation back to deck management
        [RelayCommand]
        private void BackToDeckManagement()
        {
            SelectedOracleCard = null;
            ExitEditorRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        protected void ClearSelections()
        {
            SelectedOracleCard = null;
            SelectedDeckCard = null;
        }

        // Adding a card

        [RelayCommand]
        private Task AddOracleCardToDeckAsync(object? param)
        {
            return AddOracleCardsToDeckZoneAsync(param, 1, DeckSection.Mainboard);
        }

        [RelayCommand]
        private Task AddOracleCardPlaySetToDeckAsync(object? param)
        {
            return AddOracleCardsToDeckZoneAsync(param, 4, DeckSection.Mainboard);
        }

        [RelayCommand]
        private Task AddOracleCardToSideboardAsync(object? param)
        {
            return AddOracleCardsToDeckZoneAsync(param, 1, DeckSection.Sideboard);
        }

        [RelayCommand]
        private Task AddOracleCardToMaybeboardAsync(object? param)
        {
            return AddOracleCardsToDeckZoneAsync(param, 1, DeckSection.Maybeboard);
        }

        [RelayCommand]
        private async Task SetOracleCardAsCommanderAsync(object? parameter)
        {
            var selectedCard = GetOracleCardsFromCommandParameter(parameter).FirstOrDefault();

            if (selectedCard is null || DeckLocationId is null)
            {
                return;
            }

            var result = await _deckBuilderService.SetCommanderAsync(DeckLocationId.Value, DeckFormat, CreateDeckCardStates(), selectedCard);
            ApplySuccessfulMutation(result);
        }

        [RelayCommand]
        private async Task SetOracleCardAsCompanionAsync(object? parameter)
        {
            var selectedCard = GetOracleCardsFromCommandParameter(parameter).FirstOrDefault();

            if (selectedCard is null || DeckLocationId is null)
            {
                return;
            }

            var result = await _deckBuilderService.SetCompanionAsync(DeckLocationId.Value, DeckFormat, CreateDeckCardStates(), selectedCard);

            ApplySuccessfulMutation(result);
        }


        // Add OracleCard helpers
        private async Task AddOracleCardsToDeckZoneAsync(object? parameter, int quantity, DeckSection section)
        {
            if (DeckLocationId is null)
            {
                return;
            }

            var selectedCards = GetOracleCardsFromCommandParameter(parameter).ToList();

            if (selectedCards.Count == 0)
            {
                return;
            }

            var result = await _deckBuilderService.AddCardsAsync(DeckLocationId.Value, CreateDeckCardStates(), selectedCards, quantity, section);

            ApplySuccessfulMutation(result);
        }
        private static IEnumerable<OracleCard> GetOracleCardsFromCommandParameter(object? param)
        {
            if (param is OracleCard singleCard)
            {
                yield return singleCard;
                yield break;
            }

            if (param is IEnumerable selectedItems)
            {
                foreach (var item in selectedItems)
                {
                    if (item is OracleCard card)
                    {
                        yield return card;
                    }
                }
            }
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
                RefreshZoneVisibility();
                RefreshOwnedQuantityStatus();
                return;
            }

            if (row.DesiredQuantity != updatedCard.DesiredQuantity)
            {
                row.DesiredQuantity = updatedCard.DesiredQuantity;
                RefreshOwnedQuantityStatus();
            }            
        }

        #endregion

        #region Refresh Methods
        private void RefreshAll()
        {
            RefreshZoneVisibility();
            RefreshRuleDependentProperties();
            RefreshOwnedQuantityStatus();
        }
        private void RefreshZoneVisibility()
        {
            IsCommanderZoneVisible = CommanderZone.Cards.Count > 0 && CommanderFormats.IsCommanderLike(DeckFormat);
            IsSideboardZoneVisible = SideboardZone.Cards.Count > 0;
            IsCompanionZoneVisible = CompanionZone.Cards.Count > 0;
            IsMaybeboardZoneVisible = MaybeboardZone.Cards.Count > 0;
        }
        private void RefreshRuleDependentProperties()
        {
            var availability = SelectedOracleCard is null
                ? new DeckActionAvailability()
                : _deckBuilderService.GetActionAvailability(DeckFormat, CreateDeckCardStates(), SelectedOracleCard);

            IsAddButtonVisible = SelectedOracleCard is not null;
            CanSetSelectedOracleCardAsCommander = availability.CanSetAsCommander && IsAddButtonVisible is true;
            CanSetSelectedOracleCardAsCompanion = availability.CanSetAsCompanion && IsAddButtonVisible is true;
        }
        private void RefreshOwnedQuantityStatus()
        {
            var requiredByOracleId = MainboardZone.Cards
                .Concat(SideboardZone.Cards)
                .Concat(CommanderZone.Cards)
                .Concat(CompanionZone.Cards)
                .GroupBy(row => row.OracleId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Sum(row => row.DesiredQuantity), StringComparer.OrdinalIgnoreCase);

            foreach (var row in GetAllDeckRows())
            {
                var requiredQuantity = requiredByOracleId.GetValueOrDefault(row.OracleId);

                row.HasInsufficientAvailableQuantity = row.AvailableQuantity < requiredQuantity;
            }
        }
        private IEnumerable<DeckCardEntryViewModel> GetAllDeckRows()
        {
            return MainboardZone.Cards.Concat(SideboardZone.Cards).Concat(CommanderZone.Cards).Concat(CompanionZone.Cards);
        }
        #endregion

        // Shared helpers
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

            var allocatedQuantity = DeckLocationId is int locationId? _collectionQuantitySnapshot?.GetAllocatedQuantity(oracleId, locationId) ?? 0 : 0;

            var availableQuantity = DeckLocationId is int currentLocationId
                ? _collectionQuantitySnapshot?.GetAvailableQuantity(oracleId, currentLocationId) ?? 0
                : ownedQuantity;

            return new DeckCardEntryViewModel(quantityCommitAsync: OnDeckCardQuantityCommitAsync, desiredQuantityChanged: _ => RefreshOwnedQuantityStatus())
            {
                OracleCard = card.Card,
                DesiredQuantity = card.DesiredQuantity,
                Section = card.Section,
                IsLegal = validation.IsLegal,
                OwnedQuantity = ownedQuantity,
                AllocatedQuantity = allocatedQuantity,
                AvailableQuantity = availableQuantity
            };
        }
        private Task OnDeckCardQuantityCommitAsync(DeckCardEntryViewModel? row)
        {
            if (row is null)
            {
                return Task.CompletedTask;
            }

            return SetCardQuantityAsync(row, row.DesiredQuantity);
        }
        private void ClearZones()
        {
            Debug.WriteLine("ClearZones called");

            foreach (var zone in Zones)
            {
                zone.Cards.Clear();
            }
        }
        private void AddRowToZone(DeckCardEntryViewModel row) { GetZone(row.Section).Cards.Add(row); }
        private void AddDeckRows(IReadOnlyCollection<DeckCardState> deckCards)
        {
            var rows = deckCards.Select(card => CreateDeckRow(card, deckCards)).ToList();

            foreach (var row in rows.Where(row => row.Section is DeckSection.Mainboard or DeckSection.Sideboard or DeckSection.Maybeboard)
                .OrderBy(GetCardTypeSortOrder)
                .ThenBy(row => CardColorSort.GetRank(row.OracleCard.Colors))
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
        private static int GetCardTypeSortOrder( DeckCardEntryViewModel row)
        {
            var type = row.OracleCard.Type ?? string.Empty;
            var gamePlayCard = row.OracleCard.GamePlayCard;

            Debug.WriteLine($"GetCardTypeSortOrder called for {row.CardName} with type '{type}' and GamePlayCard {gamePlayCard}");

            // Treat anything with the Land type as a land,
            // even e.g. a Land Creature.

            if (gamePlayCard == 0)
            {
                return 99;
            }

            if (type.Contains("Basic Land", StringComparison.OrdinalIgnoreCase))
            {
                return 9;
            }

            if (type.Contains("Land", StringComparison.OrdinalIgnoreCase))
            {
                return 8;
            }

            if (type.Contains("Creature", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (type.Contains("Sorcery", StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            if (type.Contains("Instant", StringComparison.OrdinalIgnoreCase))
            {
                return 3;
            }

            if (type.Contains("Enchantment", StringComparison.OrdinalIgnoreCase))
            {
                return 4;
            }

            if (type.Contains("Planeswalker", StringComparison.OrdinalIgnoreCase))
            {
                return 5;
            }

            if (type.Contains("Artifact", StringComparison.OrdinalIgnoreCase))
            {
                return 6;
            }

            return 10;
        }
        private IReadOnlyList<DeckCardState> CreateDeckCardStates()
        {
            return [.. AllDeckCards.Select(x => new DeckCardState
            {
                Card = x.OracleCard,
                DesiredQuantity = x.DesiredQuantity,
                Section = x.Section
            })];
        }


    }
}
