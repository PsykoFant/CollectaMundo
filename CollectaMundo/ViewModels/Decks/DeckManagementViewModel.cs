using CollectaMundo.ApplicationServices.CardLocations;
using CollectaMundo.ApplicationServices.CardLocations.Models;
using CollectaMundo.ApplicationServices.Decks;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Shared.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace CollectaMundo.ViewModels.Decks
{
    public partial class DeckManagementViewModel : ObservableObject
    {
        private readonly ICardLocationService _cardLocationService;
        private readonly IDeckManagementStore _deckManagementStore;

        public event EventHandler<CollectionChangeSet<CardSet>>? CollectionChanged;
        public DeckManagementViewModel(ICardLocationService cardLocationService, IDeckManagementStore deckManagementStore)
        {
            _cardLocationService = cardLocationService;
            _deckManagementStore = deckManagementStore;

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
        private string? selectedDeckFormat = string.Empty;

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

        public ObservableCollection<DeckManagementRecord> Decks => _deckManagementStore.Decks;
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
                SelectedDeckFormat = value.Format ?? string.Empty;
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

                var input = new DeckManagementInput
                {
                    Name = DeckName,
                    Format = SelectedDeckFormat,
                    Description = Description
                };

                if (IsEditing && SelectedDeck is not null)
                {
                    var mutation = await _cardLocationService.UpdateDeckAsync(SelectedDeck.LocationId, input);

                    ShowStatus(mutation.Result.Message);

                    if (mutation.Result.Code == OperationResultCode.Success && mutation.Entity is not null)
                    {
                        _deckManagementStore.Upsert(mutation.Entity);
                        ResetEditorAndSelection();
                    }

                    return;
                }

                var createMutation = await _cardLocationService.CreateDeckAsync(input);

                ShowStatus(createMutation.Result.Message);

                if (createMutation.Result.Code == OperationResultCode.Success && createMutation.Entity is not null)
                {
                    _deckManagementStore.Upsert(createMutation.Entity);
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

                var idsToDelete = SelectedDecks.Select(deck => deck.LocationId).ToList();

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
        private void ResetEditorAndSelection()
        {
            SelectedDeck = null;
            SelectedDecks.Clear();
            DeckName = string.Empty;
            SelectedDeckFormat = string.Empty;
            Description = string.Empty;
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
