using CollectaMundo.ApplicationServices.CardLocations;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.CardLocations.Models;
using CollectaMundo.DomainLogic.Shared.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace CollectaMundo.ViewModels.Utilities
{
    public partial class CardLocationViewModel : ObservableObject
    {
        private readonly ICardLocationService _cardLocationService;

        public event EventHandler<CollectionChangeSet<CardSet>>? CollectionChanged;
        public CardLocationViewModel(ICardLocationService cardLocationService)
        {
            _cardLocationService = cardLocationService;

            SelectedLocations.CollectionChanged += (_, _) =>
            {
                IsDeleteConfirmationActive = false;
                OnPropertyChanged(nameof(HasSelectedLocations));
                OnPropertyChanged(nameof(SaveEditEnabled));
            };
        }

        // Computed UI state
        public string SubmitButtonText => IsEditing ? "Save changes" : "Add location";
        public string DeleteButtonText => IsDeleteConfirmationActive ? "Yes, delete!" : "Delete selected";
        public string ModeMessage => IsDeleteConfirmationActive
            ? "Confirm delete"
            : IsEditing
                ? "Edit selected location"
                : "Add a new location";

        public bool IsEditing => SelectedLocation is not null;
        public bool HasSelectedLocations => SelectedLocations.Count > 0;
        public bool SaveEditEnabled => SelectedLocations.Count < 2 && !IsDeleteConfirmationActive;

        // Bindable state
        [ObservableProperty]
        private string statusMessage = string.Empty;

        [ObservableProperty]
        private string locationName = string.Empty;

        [ObservableProperty]
        private CardLocationType selectedLocationType = CardLocationType.Storage;

        [ObservableProperty]
        private bool isStatusVisible;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private bool isDeleteConfirmationActive;

        [ObservableProperty]
        private CardLocation? selectedLocation;

        // Collections
        public ObservableCollection<CardLocation> Locations { get; } = [];
        public ObservableCollection<CardLocation> SelectedLocations { get; } = [];
        public ObservableCollection<CardLocationType> LocationTypes { get; } =
        [
            CardLocationType.Storage,
            CardLocationType.Deck
        ];
        partial void OnSelectedLocationChanged(CardLocation? value)
        {
            if (value is not null)
            {
                LocationName = value.Name;
                SelectedLocationType = value.Type;
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

        // Load
        public async Task LoadCardLocationsAsync()
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

                var loadedLocations = (await _cardLocationService.GetAllAsync()).ToList();

                Locations.Clear();

                foreach (var location in loadedLocations)
                {
                    Locations.Add(location);
                }

                ClearStatus();
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to load card locations: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // Commands
        [RelayCommand]
        private async Task SubmitLocation()
        {
            if (IsBusy)
            {
                return;
            }

            try
            {
                IsBusy = true;
                ClearStatus();

                if (IsEditing && SelectedLocation is not null)
                {
                    var mutation = await _cardLocationService.UpdateAsync(
                        SelectedLocation.Id,
                        LocationName,
                        SelectedLocationType);

                    ShowStatus(mutation.Result.Message);

                    if (mutation.Result.Code == OperationResultCode.Success && mutation.Location is not null)
                    {
                        ReplaceLocationInCollection(mutation.Location);
                        ResetEditorAndSelection();
                    }

                    return;
                }

                var createMutation = await _cardLocationService.CreateAsync(LocationName, SelectedLocationType);

                ShowStatus(createMutation.Result.Message);

                if (createMutation.Result.Code == OperationResultCode.Success && createMutation.Location is not null)
                {
                    Locations.Add(createMutation.Location);
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
        private async Task DeleteSelectedLocations()
        {
            if (IsBusy || SelectedLocations.Count == 0)
            {
                return;
            }

            if (!IsDeleteConfirmationActive)
            {
                IsDeleteConfirmationActive = true;
                ShowStatus("This will also delete the location from cards with that location in your collection (if any).");
                return;
            }

            try
            {
                IsBusy = true;
                ClearStatus();

                var idsToDelete = SelectedLocations
                    .Select(location => location.Id)
                    .ToList();

                int deletedCount = 0;
                var failedMessages = new List<string>();

                foreach (int id in idsToDelete)
                {
                    var result = await _cardLocationService.DeleteAsync(id);

                    if (result.Result.Code == OperationResultCode.Success)
                    {
                        RemoveLocationFromCollection(id);
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
                        ? "Location deleted successfully."
                        : $"{deletedCount} locations deleted successfully.");
                }
                else if (deletedCount > 0)
                {
                    ShowStatus($"{deletedCount} locations deleted. Some deletions failed.");
                }
                else
                {
                    ShowStatus(failedMessages.FirstOrDefault() ?? "Failed to delete selected locations.");
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        // Helpers
        private void ResetEditorAndSelection()
        {
            SelectedLocation = null;
            SelectedLocations.Clear();
            LocationName = string.Empty;
            SelectedLocationType = CardLocationType.Storage;
        }
        private void ReplaceLocationInCollection(CardLocation updatedLocation)
        {
            int index = Locations
                .Select((location, i) => new { location, i })
                .FirstOrDefault(x => x.location.Id == updatedLocation.Id)?.i ?? -1;

            if (index >= 0)
            {
                Locations[index] = updatedLocation;
            }
        }
        private void RemoveLocationFromCollection(int id)
        {
            var existing = Locations.FirstOrDefault(location => location.Id == id);

            if (existing is not null)
            {
                Locations.Remove(existing);
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
