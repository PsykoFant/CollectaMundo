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

            // Any change to selection recalculates the editor mode
            // (Create / Preview / Single Edit / Multi Edit)
            SelectedLocations.CollectionChanged += (_, _) =>
            {
                IsDeleteConfirmationActive = false;
                OnPropertyChanged(nameof(HasSelectedLocations));
                RefreshSelectionMode();
            };

            SetCreateMode();
        }

        // Computed UI state
        private LocationEditorMode? editorMode;
        public bool IsCancelVisible => IsDeleteConfirmationActive || editorMode == LocationEditorMode.EditSingle;
        public bool IsEditorEnabled => editorMode is LocationEditorMode.Create or LocationEditorMode.EditSingle or LocationEditorMode.EditMultiple;
        public bool IsActionButtonEnabled => !IsBusy && !IsDeleteConfirmationActive && editorMode is
            (LocationEditorMode.Create or LocationEditorMode.SelectedReadOnly or LocationEditorMode.EditSingle or LocationEditorMode.EditMultiple);

        public bool IsNameEditorEnabled => editorMode is LocationEditorMode.Create or LocationEditorMode.EditSingle;
        public bool IsTypeEditorEnabled => editorMode is LocationEditorMode.Create or LocationEditorMode.EditSingle or LocationEditorMode.EditMultiple;
        public string ActionButtonText => editorMode switch
        {
            LocationEditorMode.Create => "Add location",
            LocationEditorMode.SelectedReadOnly => "Edit location",
            LocationEditorMode.EditSingle => "Save changes",
            LocationEditorMode.EditMultiple => "Update selected",
            _ => "Submit"
        };
        public string ModeMessage => editorMode switch
        {
            LocationEditorMode.Create => "Add a new card location",
            LocationEditorMode.SelectedReadOnly => string.Empty,
            LocationEditorMode.EditSingle => "Edit selected card location",
            LocationEditorMode.EditMultiple => "Edit selected card locations",
            _ => string.Empty
        };

        public string DeleteButtonText => IsDeleteConfirmationActive ? "Yes, delete!" : "Delete selected";

        public bool HasSelectedLocations => SelectedLocations.Count > 0;

        // Bindable state
        [ObservableProperty]
        private string statusMessage = string.Empty;

        [ObservableProperty]
        private string locationName = string.Empty;

        [ObservableProperty]
        private CardLocationType? selectedLocationType;

        [ObservableProperty]
        private bool isStatusVisible;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private bool isDeleteConfirmationActive;

        [ObservableProperty]
        private CardLocation? selectedLocation;

        [ObservableProperty]
        private int clearSelectionTrigger;

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
            IsDeleteConfirmationActive = false;

            if (value is null)
            {
                SetCreateMode();
                return;
            }

            LocationName = value.Name;
            SelectedLocationType = value.Type;
            editorMode = LocationEditorMode.SelectedReadOnly;

            RefreshEditorState();
        }
        partial void OnIsDeleteConfirmationActiveChanged(bool value)
        {
            OnPropertyChanged(nameof(ModeMessage));
            OnPropertyChanged(nameof(DeleteButtonText));
            OnPropertyChanged(nameof(IsActionButtonEnabled));
            OnPropertyChanged(nameof(IsCancelVisible));
        }
        partial void OnIsBusyChanged(bool value)
        {
            OnPropertyChanged(nameof(IsActionButtonEnabled));
            OnPropertyChanged(nameof(IsNameEditorEnabled));
            OnPropertyChanged(nameof(IsTypeEditorEnabled));
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

                var loadedLocations = (await _cardLocationService.GetAllLocationsAsync()).ToList();

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
        private void BeginEditSelectedLocation()
        {
            if (SelectedLocation is null || SelectedLocations.Count > 1)
            {
                return;
            }

            editorMode = LocationEditorMode.EditSingle;
            RefreshEditorState();
        }

        [RelayCommand]
        private async Task SubmitAction()
        {
            if (IsBusy)
            {
                return;
            }

            try
            {
                IsBusy = true;
                ClearStatus();

                if (editorMode == LocationEditorMode.SelectedReadOnly)
                {
                    BeginEditSelectedLocation();
                    return;
                }

                if (editorMode == LocationEditorMode.EditSingle && SelectedLocation is not null)
                {
                    if (SelectedLocationType is not CardLocationType locationType)
                    {
                        ShowStatus("Select a location type before saving changes.");
                        return;
                    }

                    var mutation = await _cardLocationService.UpdateLocationAsync(SelectedLocation.Id, LocationName, locationType);

                    if (mutation.Result.Code == OperationResultCode.Success && mutation.Entity is not null)
                    {
                        ReplaceLocationInCollection(mutation.Entity);
                        ResetEditorAndSelection();
                    }

                    ShowStatus(mutation.Result.Message);

                    return;
                }

                if (editorMode == LocationEditorMode.EditMultiple)
                {
                    if (SelectedLocationType is not CardLocationType locationType)
                    {
                        ShowStatus("Select a location type before updating selected locations.");
                        return;
                    }

                    foreach (var location in SelectedLocations.ToList())
                    {
                        var mutation = await _cardLocationService.UpdateLocationAsync(location.Id, location.Name, locationType);

                        if (mutation.Result.Code != OperationResultCode.Success || mutation.Entity is null)
                        {
                            ShowStatus(mutation.Result.Message);
                            return;
                        }

                        ReplaceLocationInCollection(mutation.Entity);
                    }

                    ResetEditorAndSelection();
                    ShowStatus("Selected locations updated.");
                    return;
                }

                if (SelectedLocationType is not CardLocationType createType)
                {
                    ShowStatus("Select a location type before creating a location.");
                    return;
                }

                var createMutation = await _cardLocationService.CreateLocationAsync(
                    LocationName,
                    createType);

                if (createMutation.Result.Code == OperationResultCode.Success &&
                    createMutation.Entity is not null)
                {
                    Locations.Add(createMutation.Entity);
                    ResetEditorAndSelection();
                    ShowStatus(createMutation.Result.Message);
                }
                else
                {
                    ShowStatus(createMutation.Result.Message);
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
                    var result = await _cardLocationService.DeleteLocationAsync(id);

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

        [RelayCommand]
        private void ClearSelectionAndRestoreCreateMode()
        {
            if (editorMode is not (LocationEditorMode.SelectedReadOnly or LocationEditorMode.EditMultiple))
            {
                return;
            }

            ResetEditorAndSelection();
        }

        [RelayCommand]
        private void CancelOrClearSelection()
        {
            if (editorMode != LocationEditorMode.Create)
            {
                ResetEditorAndSelection();
            }
        }

        // Helpers
        private void SetCreateMode()
        {
            editorMode = LocationEditorMode.Create;
            LocationName = string.Empty;
            SelectedLocationType = CardLocationType.Storage;
            RefreshEditorState();
        }
        private void ResetEditorAndSelection()
        {
            var previousType = SelectedLocationType;

            SelectedLocation = null;
            SelectedLocations.Clear();
            editorMode = LocationEditorMode.Create;

            LocationName = string.Empty;
            SelectedLocationType = previousType;

            StatusMessage = string.Empty;
            IsStatusVisible = false;

            ClearSelectionTrigger++;

            RefreshEditorState();
        }
        private void RefreshSelectionMode()
        {
            IsDeleteConfirmationActive = false;

            if (SelectedLocations.Count > 1)
            {
                editorMode = LocationEditorMode.EditMultiple;

                LocationName = string.Empty;
                SelectedLocationType = null;

                RefreshEditorState();
                return;
            }

            if (SelectedLocation is not null)
            {
                editorMode = LocationEditorMode.SelectedReadOnly;

                LocationName = SelectedLocation.Name;
                SelectedLocationType = SelectedLocation.Type;

                RefreshEditorState();
                return;
            }

            SetCreateMode();
        }
        private void RefreshEditorState()
        {
            OnPropertyChanged(nameof(IsCancelVisible));
            OnPropertyChanged(nameof(IsEditorEnabled));
            OnPropertyChanged(nameof(ActionButtonText));
            OnPropertyChanged(nameof(ModeMessage));
            OnPropertyChanged(nameof(DeleteButtonText));
            OnPropertyChanged(nameof(IsNameEditorEnabled));
            OnPropertyChanged(nameof(IsTypeEditorEnabled));
            OnPropertyChanged(nameof(IsActionButtonEnabled));
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
        private enum LocationEditorMode
        {
            Create,
            SelectedReadOnly,
            EditSingle,
            EditMultiple
        }
    }
}
