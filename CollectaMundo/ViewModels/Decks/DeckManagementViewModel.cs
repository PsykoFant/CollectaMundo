using CollectaMundo.ApplicationServices.CardLocations;
using CollectaMundo.ApplicationServices.CardLocations.Models;
using CollectaMundo.ApplicationServices.Decks;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Shared.Models;
using CollectaMundo.ViewModels.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace CollectaMundo.ViewModels.Decks
{
    public partial class DeckManagementViewModel(ICardLocationService cardLocationService, IDeckManagementStore deckManagementStore) : LocationManagementViewModel<DeckManagementRecord>
    {
        private readonly ICardLocationService _cardLocationService = cardLocationService;
        private readonly IDeckManagementStore _deckManagementStore = deckManagementStore;

        public event EventHandler<CollectionChangeSet<CardSet>>? CollectionChanged;

        protected override string CreateButtonText => "Add deck";
        protected override string EditButtonText => "Edit deck";
        protected override string SaveButtonText => "Save changes";
        protected override string BulkUpdateButtonText => "Update selected";

        protected override string CreateModeMessage => "Add a new deck";
        protected override string SelectedReadOnlyModeMessage => string.Empty;
        protected override string EditSingleModeMessage => "Edit selected deck";
        protected override string EditMultipleModeMessage => "Edit selected decks";

        [ObservableProperty]
        private string deckName = string.Empty;

        [ObservableProperty]
        private string? selectedDeckFormat = string.Empty;

        [ObservableProperty]
        private string description = string.Empty;
        public ObservableCollection<DeckManagementRecord> Decks => _deckManagementStore.Decks;
        public ObservableCollection<string> DeckFormats { get; } =
        [
            "commander",
            "standard",
            "modern",
            "pioneer",
            "legacy",
            "vintage",
            "pauper",
            "brawl",
            "historic"
        ];
        protected override void OnEnterCreateMode()
        {
            SelectedDeckFormat ??= string.Empty;
        }
        protected override void OnEnterSelectedReadOnlyMode(DeckManagementRecord selectedItem)
        {
            DeckName = selectedItem.Name;
            SelectedDeckFormat = selectedItem.Format ?? string.Empty;
            Description = selectedItem.Description ?? string.Empty;
        }
        protected override void OnEnterEditSingleMode(DeckManagementRecord selectedItem)
        {
            DeckName = selectedItem.Name;
            SelectedDeckFormat = selectedItem.Format ?? string.Empty;
            Description = selectedItem.Description ?? string.Empty;
        }
        protected override void OnEnterEditMultipleMode(IReadOnlyList<DeckManagementRecord> selectedItems)
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
        public async Task LoadDecksAsync()
        {
            if (IsBusy)
            {
                return;
            }

            try
            {
                IsBusy = true;
                await _deckManagementStore.LoadAsync();
                ClearStatus();
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to load decks: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }
        protected override async Task CreateAsync()
        {
            var input = new DeckManagementInput
            {
                Name = DeckName,
                Format = SelectedDeckFormat,
                Description = Description
            };

            var mutation = await _cardLocationService.CreateDeckAsync(input);

            ShowStatus(mutation.Result.Message);

            if (mutation.Result.Code == OperationResultCode.Success && mutation.Entity is not null)
            {
                _deckManagementStore.Upsert(mutation.Entity);
                ResetEditorAndSelection();
            }
        }
        protected override async Task UpdateSingleAsync(DeckManagementRecord selectedDeck)
        {
            var input = new DeckManagementInput
            {
                Name = DeckName,
                Format = SelectedDeckFormat,
                Description = Description
            };

            var mutation = await _cardLocationService.UpdateDeckAsync(
                selectedDeck.LocationId,
                input);

            ShowStatus(mutation.Result.Message);

            if (mutation.Result.Code == OperationResultCode.Success && mutation.Entity is not null)
            {
                _deckManagementStore.Upsert(mutation.Entity);
                ResetEditorAndSelection();
            }
        }
        protected override async Task UpdateMultipleAsync(IReadOnlyList<DeckManagementRecord> selectedDecks)
        {
            if (string.IsNullOrWhiteSpace(SelectedDeckFormat))
            {
                ShowStatus("Select a format before updating selected decks.");
                return;
            }

            string selectedFormat = SelectedDeckFormat;

            var updatedDecks = await _cardLocationService.UpdateDeckFormatsAsync(selectedDecks, selectedFormat);

            foreach (var updatedDeck in updatedDecks)
            {
                _deckManagementStore.Upsert(updatedDeck);
            }

            ResetEditorAndSelection();

            ShowStatus(updatedDecks.Count == 1
                ? "Deck updated successfully."
                : $"{updatedDecks.Count} decks updated successfully.");
        }

        [RelayCommand]
        private async Task DeleteSelectedDecks()
        {
            if (IsBusy || SelectedItems.Count == 0)
            {
                return;
            }

            if (!IsDeleteConfirmationActive)
            {
                IsDeleteConfirmationActive = true;
                ShowStatus("This will delete the selected deck metadata and deck location.");
                return;
            }

            try
            {
                IsBusy = true;
                ClearStatus();

                var idsToDelete = SelectedItems.Select(deck => deck.LocationId).Distinct().ToList();

                var result = await _cardLocationService.DeleteDecksAsync(idsToDelete);

                if (result.Result.Code == OperationResultCode.Success)
                {
                    foreach (int locationId in idsToDelete)
                    {
                        _deckManagementStore.Remove(locationId);
                    }

                    CollectionChanged?.Invoke(this, result.CollectionChangeSet);

                    IsDeleteConfirmationActive = false;
                    ResetEditorAndSelection();
                }

                ShowStatus(result.Result.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
