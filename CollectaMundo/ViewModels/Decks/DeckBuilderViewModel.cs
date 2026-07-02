using CollectaMundo.ApplicationServices.Decks;
using CollectaMundo.ApplicationServices.Decks.Models;
using CollectaMundo.DomainLogic.Decks.Models;
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

        // Events for external notifications
        public event EventHandler? ExitEditorRequested;

        public event EventHandler<OracleCardImageSelectionRequest?>? CardImageSelectionRequested;

        // DeckZoneViewModels for each deck section
        public DeckZoneViewModel MainboardZone => GetZone(DeckSection.Mainboard);
        public DeckZoneViewModel SideboardZone => GetZone(DeckSection.Sideboard);
        public DeckZoneViewModel CommanderZone => GetZone(DeckSection.Commander);
        public DeckZoneViewModel MaybeboardZone => GetZone(DeckSection.Maybeboard);
        private ObservableCollection<DeckZoneViewModel> Zones { get; } =
        [
            new() { Section = DeckSection.Mainboard, DisplayName = "Deck" },
            new() { Section = DeckSection.Sideboard, DisplayName = "Sideboard" },
            new() { Section = DeckSection.Commander, DisplayName = "Command zone" },
            new() { Section = DeckSection.Maybeboard, DisplayName = "Maybeboard" }
        ];
        private IEnumerable<DeckCardEntryViewModel> AllDeckCards => Zones.SelectMany(z => z.Cards);
        private DeckZoneViewModel GetZone(DeckSection section) { return Zones.First(z => z.Section == section); }
        private void AddRowToZone(DeckCardEntryViewModel row) { GetZone(row.Section).Cards.Add(row); }
        private void ClearZones()
        {
            foreach (var zone in Zones)
            {
                zone.Cards.Clear();
            }
        }

        // Filtered OracleCard list view model
        public CardListViewModel<OracleCard> CardsVM { get; } = oracleCardsVM;

        // Filter panel view model
        public FilterPanelViewModel FilterVM { get; } = filterPanelViewModel;

        // Bindable pass-through properties for the filters 
        public FilterItemViewModel? NameFilter => FilterVM.Filters.TryGetValue("Name", out var f) ? f : null;


        [ObservableProperty]
        private OracleCard? selectedOracleCard;

        [ObservableProperty]
        private DeckCardEntryViewModel? selectedDeckCard;

        [ObservableProperty]
        private int? deckLocationId;

        [ObservableProperty]
        private string deckName = string.Empty;

        [ObservableProperty]
        private string? deckFormat;

        [ObservableProperty]
        private string? deckDescription;
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
            DeckDescription = deck.Description;

            ClearZones();

            foreach (var row in rows)
            {
                AddRowToZone(row);
            }
        }

        // Selecting a card in OracleCard datagrid
        partial void OnSelectedOracleCardChanged(OracleCard? value)
        {
            if (value is null)
            {
                CardImageSelectionRequested?.Invoke(this, new OracleCardImageSelectionRequest());
                return;
            }

            CardImageSelectionRequested?.Invoke(this, new OracleCardImageSelectionRequest(OracleId: value.ScryfallOracleId, Name: value.Name));
        }

        // Selecting a card in deck datagrid
        partial void OnSelectedDeckCardChanged(DeckCardEntryViewModel? value)
        {
            if (value is null)
            {
                CardImageSelectionRequested?.Invoke(this, new OracleCardImageSelectionRequest());
                return;
            }

            CardImageSelectionRequested?.Invoke(
                this,
                new OracleCardImageSelectionRequest(
                    OracleId: value.OracleId,
                    Name: value.CardName));
        }

        // Adding a card

        [RelayCommand]
        private Task AddOracleCardToDeckAsync(object? param)
        {
            return AddOracleCardsQuantityToDeckAsync(param, quantityToAdd: 1);
        }

        [RelayCommand]
        private Task AddOracleCardPlaySetToDeckAsync(object? param)
        {
            return AddOracleCardsQuantityToDeckAsync(param, quantityToAdd: 4);
        }

        // Add OracleCard helpers
        private async Task AddOracleCardsQuantityToDeckAsync(object? param, int quantityToAdd)
        {
            var cards = GetOracleCardsFromCommandParameter(param).ToList();

            if (cards.Count == 0 || DeckLocationId is null || quantityToAdd <= 0)
            {
                return;
            }

            var addedRows = new List<DeckCardEntryViewModel>();
            var changedRows = new List<(DeckCardEntryViewModel Row, int PreviousQuantity)>();

            try
            {
                foreach (var card in cards)
                {
                    var zone = GetZone(DeckSection.Mainboard);
                    var existing = zone.Cards.FirstOrDefault(x => x.OracleId == card.ScryfallOracleId);

                    if (existing is not null)
                    {
                        changedRows.Add((existing, existing.DesiredQuantity));
                        existing.DesiredQuantity += quantityToAdd;
                    }
                    else
                    {
                        var addedRow = CreateDeckRow(card, quantityToAdd, DeckSection.Mainboard);

                        zone.Cards.Add(addedRow);
                        addedRows.Add(addedRow);
                    }
                }

                await PersistDeckAsync();
            }
            catch (Exception ex)
            {
                foreach (var (row, previousQuantity) in changedRows)
                {
                    row.DesiredQuantity = previousQuantity;
                }

                foreach (var addedRow in addedRows)
                {
                    GetZone(addedRow.Section).Cards.Remove(addedRow);
                }

                Debug.WriteLine($"Failed to add cards to deck: {ex}");
                throw;
            }
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
            return new DeckCardEntryViewModel(
                quantityCommitAsync: OnDeckCardQuantityCommitAsync)
            {
                OracleCard = card,
                DesiredQuantity = desiredQuantity,
                Section = section
            };
        }
        private async Task OnDeckCardQuantityCommitAsync(DeckCardEntryViewModel row)
        {
            if (row.DesiredQuantity <= 0)
            {
                await DeleteDeckCardsAsync([row]);
                return;
            }

            await PersistDeckAsync();
        }

        // Deleting a card
        [RelayCommand]
        private async Task DeleteDeckCardsAsync(IReadOnlyList<DeckCardEntryViewModel> rows)
        {
            var removed = rows.Select(row => new
            {
                Row = row,
                Zone = GetZone(row.Section),
                Index = GetZone(row.Section).Cards.IndexOf(row)
            }).Where(x => x.Index >= 0).OrderByDescending(x => x.Index).ToList();

            if (removed.Count == 0)
            {
                return;
            }

            try
            {
                foreach (var item in removed)
                {
                    item.Zone.Cards.RemoveAt(item.Index);
                }

                await PersistDeckAsync();
            }
            catch (Exception ex)
            {
                foreach (var item in removed.OrderBy(x => x.Index))
                {
                    item.Zone.Cards.Insert(item.Index, item.Row);
                }

                Debug.WriteLine($"Failed to delete deck card entries: {ex}");
                throw;
            }
        }
        private static IEnumerable<DeckCardEntryViewModel> GetDeckRowsFromCommandParameter(object? param)
        {
            if (param is DeckCardEntryViewModel singleRow)
            {
                yield return singleRow;
                yield break;
            }

            if (param is System.Collections.IEnumerable selectedItems)
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

        // Persist in db
        private Task PersistDeckAsync()
        {
            if (DeckLocationId is null)
            {
                return Task.CompletedTask;
            }

            var entries = AllDeckCards.Select(x => new DeckCardEntry
            {
                DeckLocationId = DeckLocationId.Value,
                OracleId = x.OracleId,
                CardName = x.CardName,
                DesiredQuantity = x.DesiredQuantity,
                Section = x.Section
            }).ToList();

            return _deckBuilderService.SaveDeckAsync(DeckLocationId.Value, entries);
        }

        [RelayCommand]
        private void BackToDeckManagement()
        {
            SelectedOracleCard = null;
            ExitEditorRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private async Task IncrementDeckCardQuantityAsync(DeckCardEntryViewModel? row)
        {
            if (row is null)
            {
                return;
            }

            row.DesiredQuantity++;
            await PersistDeckAsync();
        }

        [RelayCommand]
        private async Task DecrementDeckCardQuantityAsync(DeckCardEntryViewModel? row)
        {
            if (row is null || row.DesiredQuantity <= 0)
            {
                return;
            }

            row.DesiredQuantity--;

            if (row.DesiredQuantity <= 0)
            {
                await DeleteDeckCardsAsync([row]);
                return;
            }

            await PersistDeckAsync();
        }


    }
}
