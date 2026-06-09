using CollectaMundo.ApplicationServices.CardLocations;
using CollectaMundo.ApplicationServices.CardLocations.Models;
using CollectaMundo.ApplicationServices.Decks;
using CollectaMundo.ApplicationServices.Decks.Models;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Shared.Models;
using CollectaMundo.ViewModels.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace CollectaMundo.ViewModels.Decks
{
    public partial class DeckManagementViewModel(ICardLocationService cardLocationService, IDeckManagementStore deckManagementStore) : LocationManagementViewModel<DeckManagementRowViewModel>
    {
        private readonly ICardLocationService _cardLocationService = cardLocationService;
        private readonly IDeckManagementStore _deckManagementStore = deckManagementStore;

        public event EventHandler<DeckManagementRowViewModel>? EditDeckRequested;

        // UI text
        protected override LocationManagementText Text { get; } = new(
            CreateText: "Add deck",
            EditText: "Edit deck",
            SaveText: "Save changes",
            BulkUpdateText: "Update selected",
            CreateMode: "Add a new deck",
            SelectedReadOnlyMode: string.Empty,
            EditSingleMode: "Edit selected deck",
            EditMultipleMode: "Edit selected decks");

        [ObservableProperty]
        private string deckName = string.Empty;

        [ObservableProperty]
        private string? selectedDeckFormat = string.Empty;

        [ObservableProperty]
        private string description = string.Empty;

        // View data
        public ObservableCollection<DeckManagementRowViewModel> Decks { get; } = [];
        public ObservableCollection<DeckFormatOption> DeckFormats => _deckManagementStore.DeckFormats;

        // Editor state hooks
        protected override void LoadEditorFromItem(DeckManagementRowViewModel selectedItem)
        {
            DeckName = selectedItem.Name;
            SelectedDeckFormat = selectedItem.Format ?? string.Empty;
            Description = selectedItem.Description ?? string.Empty;
        }
        protected override void OnEnterEditMultipleMode(IReadOnlyList<DeckManagementRowViewModel> selectedItems)
        {
            DeckName = string.Empty;
            Description = string.Empty;
            SelectedDeckFormat = null;
        }
        protected override void ClearEditorFields()
        {
            DeckName = string.Empty;
            Description = string.Empty;
            SelectedDeckFormat = string.Empty;
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
            },
            "Failed to load decks");
        }
        private DeckManagementRowViewModel CreateRow(DeckManagementRecord record)
        {
            return new DeckManagementRowViewModel(record, GetDeckFormatDisplayName);
        }
        private string GetDeckFormatDisplayName(string? format)
        {
            if (string.IsNullOrWhiteSpace(format))
            {
                return string.Empty;
            }

            return DeckFormats
                .FirstOrDefault(option => option.Value == format)
                ?.DisplayName
                ?? format;
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

            ResetEditorAndSelection();

            ShowStatus(updatedDecks.Count == 1
                ? "Deck updated successfully."
                : $"{updatedDecks.Count} decks updated successfully.");
        }


        // Commands
        [RelayCommand]
        private Task DeleteSelectedDecks()
        {
            return DeleteSelectedItemsAsync(
                "This will delete the selected deck metadata and deck location.",
                async selectedDecks =>
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

                        CollectionChanged?.Invoke(this, result.CollectionChangeSet);
                    }

                    ShowStatus(result.Result.Message);

                    return result.Result.Code is OperationResultCode.Success;
                });
        }

        [RelayCommand]
        private void EditSelectedDeck()
        {
            if (SelectedItem is null)
            {
                return;
            }

            EditDeckRequested?.Invoke(this, SelectedItem);
        }

        // External notifications
        public event EventHandler<CollectionChangeSet<CardSet>>? CollectionChanged;

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
    }
}
