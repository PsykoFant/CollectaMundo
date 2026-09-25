using CollectaMundo.ApplicationServices.CardLocations;
using CollectaMundo.ApplicationServices.Decks;
using CollectaMundo.ApplicationServices.Decks.DeckExport;
using CollectaMundo.ApplicationServices.Decks.Models;
using CollectaMundo.ApplicationServices.Shared.Operation;
using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.DomainLogic.Shared.Models;
using CollectaMundo.Infrastructure.Shared.Models;
using CollectaMundo.ViewModels.Decks.Models;
using CollectaMundo.ViewModels.Decks.Models.RowViewModels;
using CollectaMundo.ViewModels.Shared;
using CollectaMundo.ViewModels.Shell;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace CollectaMundo.ViewModels.Decks
{
    public partial class DeckManagementViewModel(ICardLocationService cardLocationService, IDeckManagementStore deckManagementStore, IDeckExportService deckExportService, ICardCollectionHost cardCollectionHost, Func<IReadOnlyList<OracleCard>> oracleCardsProvider) : LocationManagementViewModel<DeckManagementRowViewModel>
    {
        private readonly ICardLocationService _cardLocationService = cardLocationService;
        private readonly IDeckManagementStore _deckManagementStore = deckManagementStore;
        private readonly IDeckExportService _deckExportService = deckExportService;
        private readonly ICardCollectionHost _cardCollectionHost = cardCollectionHost;
        private readonly Func<IReadOnlyList<OracleCard>> _oracleCardsProvider = oracleCardsProvider;

        private int _exportAvailabilityRequestVersion;

        // External notifications
        public event EventHandler<CollectionChangeSet<CollectionCardDbRow>>? CollectionChanged;
        public event EventHandler<DeckManagementRowViewModel>? EditDeckRequested;

        // UI text
        protected override LocationManagementText Text { get; } = new(
            CreateText: "Add deck",
            EditText: "Edit deck metadata",
            SaveText: "Save changes",
            BulkUpdateText: "Update selected",
            CreateMode: "Add a new deck",
            SelectedReadOnlyMode: string.Empty,
            EditSingleMode: "Edit selected deck metadata",
            EditMultipleMode: "Edit selected decks");

        [ObservableProperty]
        private string deckName = string.Empty;

        [ObservableProperty]
        private string? selectedDeckFormat = string.Empty;

        [ObservableProperty]
        private string description = string.Empty;

        // UI state
        [ObservableProperty]
        private bool isEnterDeckBuilderButtonEnabled = false;

        [ObservableProperty]
        private int refreshColumnsTrigger;

        [ObservableProperty]
        private bool canExportCsv;

        [ObservableProperty]
        private bool canGenerateCardmarketWantList;

        // View data
        public ObservableCollection<DeckManagementRowViewModel> Decks { get; } = [];
        public ObservableCollection<DeckFormatOption> DeckFormats => _deckManagementStore.DeckFormats;

        // Editor state hooks
        protected override void OnEnterEditSingleMode(DeckManagementRowViewModel selectedItem)
        {
            DeckName = selectedItem.Name;
            SelectedDeckFormat = selectedItem.Format ?? string.Empty;
            Description = selectedItem.Description ?? string.Empty;
            IsEnterDeckBuilderButtonEnabled = true;

            BeginExportAvailabilityRefresh(selectedItem);
        }
        protected override void OnEnterEditMultipleMode(IReadOnlyList<DeckManagementRowViewModel> selectedItems)
        {
            DeckName = string.Empty;
            Description = string.Empty;
            SelectedDeckFormat = null;
            IsEnterDeckBuilderButtonEnabled = false;

            ClearExportAvailability();
        }
        protected override void ClearEditorFields()
        {
            DeckName = string.Empty;
            Description = string.Empty;
            SelectedDeckFormat = string.Empty;

            ClearExportAvailability();
        }

        // Data loading
        public Task LoadDecksAsync()
        {
            return RunBusyOperationAsync(async () =>
            {
                await _deckManagementStore.LoadAsync();

                Decks.Clear();

                foreach (var deck in _deckManagementStore.Decks)
                {
                    Decks.Add(CreateRow(deck));
                }

                RefreshColumns();
            },
            "Failed to load decks");
        }
        private DeckManagementRowViewModel CreateRow(DeckManagementRecord record)
        {
            var formatOption = string.IsNullOrWhiteSpace(record.Format)
                ? null
                : DeckFormats.FirstOrDefault(option => option.Value == record.Format);

            return new DeckManagementRowViewModel(record, formatOption);
        }

        // CRUD operations
        protected override async Task CreateAsync()
        {
            var input = CreateInput();
            var mutation = await _cardLocationService.CreateDeckAsync(input);

            ShowStatus(mutation.Result.Message);

            if (mutation.Result.Code == OperationResultCode.Success && mutation.Entity is not null)
            {
                UpsertDeckRow(mutation.Entity);
                RefreshColumns();
                ResetEditorAndSelection();
            }
        }
        protected override async Task UpdateSingleAsync(DeckManagementRowViewModel selectedDeck)
        {
            var mutation = await _cardLocationService.UpdateDeckAsync(selectedDeck.LocationId, CreateInput());

            ShowStatus(mutation.Result.Message);

            if (mutation.Result.Code == OperationResultCode.Success && mutation.Entity is not null)
            {
                UpsertDeckRow(mutation.Entity);
                RefreshColumns();
                ResetEditorAndSelection();
            }
        }
        protected override async Task UpdateMultipleAsync(IReadOnlyList<DeckManagementRowViewModel> selectedDecks)
        {
            if (string.IsNullOrWhiteSpace(SelectedDeckFormat))
            {
                ShowStatus("Select a format before updating selected decks.");
                return;
            }

            var selectedRecords = selectedDecks.Select(row => row.Record).ToList();

            var updatedDecks = await _cardLocationService.UpdateDeckFormatsAsync(selectedRecords, SelectedDeckFormat);

            foreach (var updatedDeck in updatedDecks)
            {
                UpsertDeckRow(updatedDeck);
            }

            RefreshColumns();
            ResetEditorAndSelection();

            ShowStatus(updatedDecks.Count == 1
                ? "Deck updated successfully."
                : $"{updatedDecks.Count} decks updated successfully.");
        }

        // Commands
        [RelayCommand]
        private Task DeleteSelectedDecks()
        {
            IsEnterDeckBuilderButtonEnabled = false;

            return DeleteSelectedItemsAsync("This will delete the selected deck metadata and deck location.", async selectedDecks =>
            {
                var idsToDelete = selectedDecks.Select(deck => deck.LocationId).Distinct().ToList();
                var entityName = idsToDelete.Count == 1 ? "deck" : "decks";
                var result = await _cardLocationService.DeleteLocationsAsync(idsToDelete, entityName);

                if (result.Result.Code is OperationResultCode.Success)
                {
                    foreach (int locationId in idsToDelete)
                    {
                        RemoveDeckRow(locationId);
                    }

                    RefreshColumns();
                    CollectionChanged?.Invoke(this, result.CollectionChangeSet);
                }

                ShowStatus(result.Result.Message);

                return result.Result.Code is OperationResultCode.Success;
            });
        }

        [RelayCommand]
        private void EnterDeckBuilder()
        {
            if (SelectedItem is null)
            {
                return;
            }

            EditDeckRequested?.Invoke(this, SelectedItem);
            ResetEditorAndSelection();
        }

        // Helper methods
        private DeckManagementInput CreateInput()
        {
            return new DeckManagementInput
            {
                Name = DeckName,
                Format = SelectedDeckFormat,
                Description = Description
            };
        }
        private void UpsertDeckRow(DeckManagementRecord deck)
        {
            _deckManagementStore.Upsert(deck);

            int index = Decks
                .Select((row, i) => new { row, i })
                .FirstOrDefault(x => x.row.LocationId == deck.LocationId)
                ?.i ?? -1;

            var row = CreateRow(deck);

            if (index >= 0)
            {
                Decks[index] = row;
                return;
            }

            Decks.Add(row);
        }
        private void RemoveDeckRow(int locationId)
        {
            _deckManagementStore.Remove(locationId);

            var existing = Decks.FirstOrDefault(row => row.LocationId == locationId);

            if (existing is not null)
            {
                Decks.Remove(existing);
            }
        }
        private void RefreshColumns()
        {
            RefreshColumnsTrigger++;
        }
        private void BeginExportAvailabilityRefresh(DeckManagementRowViewModel selectedDeck)
        {
            CanExportCsv = false;
            CanGenerateCardmarketWantList = false;

            var requestVersion = ++_exportAvailabilityRequestVersion;

            _ = RefreshExportAvailabilityAsync(selectedDeck.LocationId, requestVersion);
        }
        private async Task RefreshExportAvailabilityAsync(int deckLocationId, int requestVersion)
        {
            try
            {
                var quantitySnapshot = _cardCollectionHost.CreateCollectionQuantitySnapshot();

                var availability = await _deckExportService.GetAvailabilityAsync(deckLocationId, _oracleCardsProvider(), quantitySnapshot);

                // Selection may have changed while awaiting the DB read.
                if (requestVersion != _exportAvailabilityRequestVersion || SelectedItem?.LocationId != deckLocationId)
                {
                    return;
                }

                CanExportCsv = availability.CanExportCsv;

                CanGenerateCardmarketWantList = availability.CanGenerateCardmarketWantList;

                Debug.WriteLine($"Deck export availability refreshed: CanExportCsv={CanExportCsv}, CanGenerateCardmarketWantList={CanGenerateCardmarketWantList}");
            }
            catch (Exception ex)
            {
                // Ignore failures belonging to an old selection.
                if (requestVersion != _exportAvailabilityRequestVersion)
                {
                    return;
                }

                CanExportCsv = false;
                CanGenerateCardmarketWantList = false;

                ShowStatus($"Failed to determine deck export availability: {ex.Message}");
            }
        }
        private void ClearExportAvailability()
        {
            // Invalidates an availability calculation that may still be running.
            _exportAvailabilityRequestVersion++;

            CanExportCsv = false;
            CanGenerateCardmarketWantList = false;
        }
    }
}
