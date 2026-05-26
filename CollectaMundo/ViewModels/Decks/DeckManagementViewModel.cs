using CollectaMundo.ApplicationServices.Decks;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.Decks.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace CollectaMundo.ViewModels.Decks
{
    public partial class DeckManagementViewModel : ObservableObject
    {
        private readonly IDeckManagementService _deckManagementService;

        public DeckManagementViewModel(IDeckManagementService deckManagementService)
        {
            _deckManagementService = deckManagementService;

            SelectedDecks.CollectionChanged += (_, _) =>
            {
                IsDeleteConfirmationActive = false;
                OnPropertyChanged(nameof(HasSelectedDecks));
                OnPropertyChanged(nameof(SaveEditEnabled));
            };
        }
        public string SubmitButtonText => IsEditing ? "Save changes" : "Add deck";
        public string DeleteButtonText => IsDeleteConfirmationActive ? "Yes, delete!" : "Delete selected";

        public string ModeMessage => IsDeleteConfirmationActive
            ? "Confirm delete"
            : IsEditing
                ? "Edit selected deck"
                : "Add a new deck";

        public bool IsEditing => SelectedDeck is not null;
        public bool HasSelectedDecks => SelectedDecks.Count > 0;
        public bool SaveEditEnabled => SelectedDecks.Count < 2 && !IsDeleteConfirmationActive;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        [ObservableProperty]
        private string deckName = string.Empty;

        [ObservableProperty]
        private string selectedDeckFormat = string.Empty;

        [ObservableProperty]
        private string description = string.Empty;

        [ObservableProperty]
        private bool isStatusVisible;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private bool isDeleteConfirmationActive;

        [ObservableProperty]
        private DeckManagementRecord? selectedDeck;

        public ObservableCollection<DeckManagementRecord> Decks { get; } = [];
        public ObservableCollection<DeckManagementRecord> SelectedDecks { get; } = [];

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

        partial void OnSelectedDeckChanged(DeckManagementRecord? value)
        {
            if (value is not null)
            {
                DeckName = value.Name;
                SelectedDeckFormat = value.Format;
                Description = value.Description ?? string.Empty;
            }

            OnPropertyChanged(nameof(IsEditing));
            OnPropertyChanged(nameof(SubmitButtonText));
            OnPropertyChanged(nameof(ModeMessage));
            OnPropertyChanged(nameof(SaveEditEnabled));
        }

        partial void OnIsDeleteConfirmationActiveChanged(bool value)
        {
            OnPropertyChanged(nameof(ModeMessage));
            OnPropertyChanged(nameof(DeleteButtonText));
            OnPropertyChanged(nameof(SaveEditEnabled));
        }

        public async Task LoadDecksAsync()
        {
            if (IsBusy)
            {
                return;
            }

            await LoadInternalAsync();
        }

        private async Task LoadInternalAsync()
        {
            try
            {
                IsBusy = true;

                var loadedDecks = (await _deckManagementService.GetAllAsync()).ToList();

                Decks.Clear();

                foreach (var deck in loadedDecks)
                {
                    Decks.Add(deck);
                }

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

                var input = new DeckManagementInput
                {
                    Name = DeckName,
                    Format = SelectedDeckFormat,
                    Description = Description
                };

                if (IsEditing && SelectedDeck is not null)
                {
                    var mutation = await _deckManagementService.UpdateAsync(SelectedDeck.LocationId, input);

                    ShowStatus(mutation.Result.Message);

                    if (mutation.Result.Code == OperationResultCode.Success && mutation.Deck is not null)
                    {
                        ReplaceDeckInCollection(mutation.Deck);
                        ResetEditorAndSelection();
                    }

                    return;
                }

                var createMutation = await _deckManagementService.CreateAsync(input);

                ShowStatus(createMutation.Result.Message);

                if (createMutation.Result.Code == OperationResultCode.Success && createMutation.Deck is not null)
                {
                    Decks.Add(createMutation.Deck);
                    ResetEditorAndSelection();
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void CancelEdit()
        {
            IsDeleteConfirmationActive = false;
            ResetEditorAndSelection();
            ClearStatus();
        }

        [RelayCommand]
        private async Task DeleteSelectedDecks()
        {
            if (IsBusy || SelectedDecks.Count == 0)
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

                var idsToDelete = SelectedDecks
                    .Select(deck => deck.LocationId)
                    .ToList();

                int deletedCount = 0;
                var failedMessages = new List<string>();

                foreach (int locationId in idsToDelete)
                {
                    var result = await _deckManagementService.DeleteAsync(locationId);

                    if (result.Code == OperationResultCode.Success)
                    {
                        RemoveDeckFromCollection(locationId);
                        deletedCount++;
                    }
                    else
                    {
                        failedMessages.Add(result.Message);
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

        private void ResetEditorAndSelection()
        {
            SelectedDeck = null;
            SelectedDecks.Clear();
            DeckName = string.Empty;
            SelectedDeckFormat = string.Empty;
            Description = string.Empty;
        }

        private void ReplaceDeckInCollection(DeckManagementRecord updatedDeck)
        {
            int index = Decks
                .Select((deck, i) => new { deck, i })
                .FirstOrDefault(x => x.deck.LocationId == updatedDeck.LocationId)?.i ?? -1;

            if (index >= 0)
            {
                Decks[index] = updatedDeck;
            }
        }

        private void RemoveDeckFromCollection(int locationId)
        {
            var existing = Decks.FirstOrDefault(deck => deck.LocationId == locationId);

            if (existing is not null)
            {
                Decks.Remove(existing);
            }
        }

        private void ClearStatus()
        {
            StatusMessage = string.Empty;
            IsStatusVisible = false;
        }

        private void ShowStatus(string message)
        {
            StatusMessage = message;
            IsStatusVisible = !string.IsNullOrWhiteSpace(message);
        }
    }
}
