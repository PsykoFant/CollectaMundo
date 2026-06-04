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

        [RelayCommand]
        private async Task SubmitDeck()
        {
            if (IsBusy)
            {
                return;
            }

            try
            {
                IsBusy = true;
                ClearStatus();

                if (EditorMode is SelectionEditorMode.SelectedReadOnly)
                {
                    BeginEditSelectedItemCommand.Execute(null);
                    return;
                }

                if (EditorMode is SelectionEditorMode.EditSingle && SelectedItem is not null)
                {
                    await UpdateSingleDeckAsync(SelectedItem);
                    return;
                }

                if (EditorMode is SelectionEditorMode.EditMultiple)
                {
                    await UpdateSelectedDeckFormatsAsync();
                    return;
                }

                await CreateDeckAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }
        private async Task CreateDeckAsync()
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
        private async Task UpdateSingleDeckAsync(DeckManagementRecord selectedDeck)
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
        private async Task UpdateSelectedDeckFormatsAsync()
        {
            if (string.IsNullOrWhiteSpace(SelectedDeckFormat))
            {
                ShowStatus("Select a format before updating selected decks.");
                return;
            }

            var selectedDecks = SelectedItems.ToList();
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

                var idsToDelete = SelectedItems
                    .Select(deck => deck.LocationId)
                    .ToList();

                int deletedCount = 0;
                var failedMessages = new List<string>();

                foreach (int locationId in idsToDelete)
                {
                    var result = await _cardLocationService.DeleteDeckAsync(locationId);

                    if (result.Result.Code == OperationResultCode.Success)
                    {
                        _deckManagementStore.Remove(locationId);
                        CollectionChanged?.Invoke(this, result.CollectionChangeSet);
                        deletedCount++;
                    }
                    else
                    {
                        failedMessages.Add(result.Result.Message);
                    }
                }

                IsDeleteConfirmationActive = false;
                ResetEditorAndSelection();

                if (failedMessages.Count == 0)
                {
                    ShowStatus(deletedCount == 1
                        ? "Deck deleted successfully."
                        : $"{deletedCount} decks deleted successfully.");
                }
                else if (deletedCount > 0)
                {
                    ShowStatus($"{deletedCount} decks deleted. Some deletions failed.");
                }
                else
                {
                    ShowStatus(failedMessages.FirstOrDefault() ?? "Failed to delete selected decks.");
                }
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
