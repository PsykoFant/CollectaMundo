using CollectaMundo.ApplicationServices.Decks;
using CollectaMundo.ApplicationServices.Decks.Models;
using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.DomainLogic.Decks.Models.Enums;
using CollectaMundo.DomainLogic.Decks.Models.Records;
using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.ViewModels.CardLists;
using CollectaMundo.ViewModels.Decks.Models;
using CollectaMundo.ViewModels.Filtering;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace CollectaMundo.ViewModels.Decks
{
    public partial class DeckBuilderViewModel(IDeckBuilderService deckBuilderService, CardListViewModel<OracleCard> oracleCardsVM, FilterPanelViewModel filterPanelViewModel) : ObservableObject
    {
        private readonly IDeckBuilderService _deckBuilderService = deckBuilderService;
        private readonly CardListViewModel<OracleCard> _oracleCardsVM = oracleCardsVM;
        private readonly FilterPanelViewModel _filterPanelViewModel = filterPanelViewModel;

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

            var rows = new List<DeckCardEntryViewModel>();

            foreach (var entry in entries)
            {
                var oracleCard = CardsVM.Cards.FirstOrDefault(c => c.ScryfallOracleId == entry.OracleId);

                if (oracleCard is null)
                {
                    continue;
                }

                rows.Add(CreateDeckRow(oracleCard, entry.DesiredQuantity, entry.Section));
            }

            DeckLocationId = deck.LocationId;
            DeckName = deck.Name;
            DeckFormat = deck.Format;

            ClearZones();

            foreach (var row in rows)
            {
                AddRowToZone(row);
            }

            RefreshZoneVisibility();
            RefreshRuleDependentProperties();
        }

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
            return AddOracleCardsQuantityToDeckAsync(param, 1, DeckSection.Mainboard);
        }

        [RelayCommand]
        private Task AddOracleCardPlaySetToDeckAsync(object? param)
        {
            return AddOracleCardsQuantityToDeckAsync(param, 4, DeckSection.Mainboard);
        }

        [RelayCommand]
        private Task AddOracleCardToSideboardAsync(object? param)
        {
            return AddOracleCardsQuantityToDeckAsync(param, 1, DeckSection.Sideboard);
        }

        [RelayCommand]
        private Task AddOracleCardToMaybeboardAsync(object? param)
        {
            return AddOracleCardsQuantityToDeckAsync(param, 1, DeckSection.Maybeboard);
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
        private async Task AddOracleCardsQuantityToDeckAsync(object? parameter, int quantity, DeckSection section)
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

        // DeckCardEntryViewModel factory
        private DeckCardEntryViewModel CreateDeckRow(OracleCard card, int desiredQuantity, DeckSection section)
        {
            return new DeckCardEntryViewModel(quantityCommitAsync: OnDeckCardQuantityCommitAsync)
            {
                OracleCard = card,
                DesiredQuantity = desiredQuantity,
                Section = section
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
                return;
            }

            if (row.DesiredQuantity != updatedCard.DesiredQuantity)
            {
                row.DesiredQuantity = updatedCard.DesiredQuantity;
            }

        }

        // Shared helpers
        private void ApplySuccessfulMutation(DeckMutationResult result)
        {
            if (!result.Succeeded)
            {
                Debug.WriteLine($"Deck mutation failed: {result.Message}");

                return;
            }

            ClearZones();

            foreach (var card in result.Cards)
            {
                AddRowToZone(CreateDeckRow(card.Card, card.DesiredQuantity, card.Section));
            }

            RefreshZoneVisibility();
            RefreshRuleDependentProperties();
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
        private IReadOnlyList<DeckCardState> CreateDeckCardStates()
        {
            return [.. AllDeckCards.Select(x => new DeckCardState
            {
                Card = x.OracleCard,
                DesiredQuantity = x.DesiredQuantity,
                Section = x.Section
            })];
        }
        private IEnumerable<DeckCardEntryViewModel> AllDeckCards => Zones.SelectMany(z => z.Cards);
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
                : _deckBuilderService.GetActionAvailability(DeckFormat,CreateDeckCardStates(),SelectedOracleCard);

            IsAddButtonVisible = SelectedOracleCard is not null;
            CanSetSelectedOracleCardAsCommander = availability.CanSetAsCommander && IsAddButtonVisible is true;
            CanSetSelectedOracleCardAsCompanion = availability.CanSetAsCompanion && IsAddButtonVisible is true;
        }







    }
}
