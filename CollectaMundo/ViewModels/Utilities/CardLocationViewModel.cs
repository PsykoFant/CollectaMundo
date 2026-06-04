using CollectaMundo.ApplicationServices.CardLocations;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.CardLocations.Models;
using CollectaMundo.DomainLogic.Shared.Models;
using CollectaMundo.ViewModels.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace CollectaMundo.ViewModels.Utilities
{
    public partial class CardLocationViewModel(ICardLocationService cardLocationService) : LocationManagementViewModel<CardLocation>
    {
        private readonly ICardLocationService _cardLocationService = cardLocationService;

        public event EventHandler<CollectionChangeSet<CardSet>>? CollectionChanged;

        protected override string CreateButtonText => "Add location";
        protected override string EditButtonText => "Edit location";
        protected override string SaveButtonText => "Save changes";
        protected override string BulkUpdateButtonText => "Update selected";

        protected override string CreateModeMessage => "Add a new card location";
        protected override string SelectedReadOnlyModeMessage => string.Empty;
        protected override string EditSingleModeMessage => "Edit selected card location";
        protected override string EditMultipleModeMessage => "Edit selected card locations";

        [ObservableProperty]
        private string locationName = string.Empty;

        [ObservableProperty]
        private CardLocationType? selectedLocationType;
        public ObservableCollection<CardLocation> Locations { get; } = [];
        public ObservableCollection<CardLocationType> LocationTypes { get; } =
        [
            CardLocationType.Storage,
            CardLocationType.Deck
        ];
        protected override void OnEnterCreateMode()
        {
            SelectedLocationType ??= CardLocationType.Storage;
        }
        protected override void OnEnterSelectedReadOnlyMode(CardLocation selectedItem)
        {
            LocationName = selectedItem.Name;
            SelectedLocationType = selectedItem.Type;
        }
        protected override void OnEnterEditSingleMode(CardLocation selectedItem)
        {
            LocationName = selectedItem.Name;
            SelectedLocationType = selectedItem.Type;
        }
        protected override void OnEnterEditMultipleMode(IReadOnlyList<CardLocation> selectedItems)
        {
            LocationName = string.Empty;
            SelectedLocationType = null;
        }
        protected override void ClearEditorFields()
        {
            LocationName = string.Empty;
        }
        public async Task LoadCardLocationsAsync()
        {
            if (IsBusy)
            {
                return;
            }

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

                if (EditorMode is SelectionEditorMode.SelectedReadOnly)
                {
                    BeginEditSelectedItemCommand.Execute(null);
                    return;
                }

                if (EditorMode is SelectionEditorMode.EditSingle && SelectedItem is not null)
                {
                    if (SelectedLocationType is not CardLocationType locationType)
                    {
                        ShowStatus("Select a location type before saving changes.");
                        return;
                    }

                    var mutation = await _cardLocationService.UpdateLocationAsync(
                        SelectedItem.Id,
                        LocationName,
                        locationType);

                    if (mutation.Result.Code is OperationResultCode.Success && mutation.Entity is not null)
                    {
                        ReplaceLocationInCollection(mutation.Entity);
                        ResetEditorAndSelection();
                    }

                    ShowStatus(mutation.Result.Message);
                    return;
                }

                if (EditorMode is SelectionEditorMode.EditMultiple)
                {
                    if (SelectedLocationType is not CardLocationType locationType)
                    {
                        ShowStatus("Select a location type before updating selected locations.");
                        return;
                    }

                    var ids = SelectedItems.Select(location => location.Id).ToList();

                    var updatedLocations = await _cardLocationService.UpdateLocationTypesAsync(ids, locationType);

                    foreach (var updatedLocation in updatedLocations)
                    {
                        ReplaceLocationInCollection(updatedLocation);
                    }

                    ResetEditorAndSelection();

                    ShowStatus(updatedLocations.Count == 1
                        ? "Location updated successfully."
                        : $"{updatedLocations.Count} locations updated successfully.");

                    return;
                }

                if (SelectedLocationType is not CardLocationType createType)
                {
                    ShowStatus("Select a location type before creating a location.");
                    return;
                }

                var createMutation = await _cardLocationService.CreateLocationAsync(LocationName, createType);

                if (createMutation.Result.Code is OperationResultCode.Success && createMutation.Entity is not null)
                {
                    Locations.Add(createMutation.Entity);
                    ResetEditorAndSelection();
                }

                ShowStatus(createMutation.Result.Message);
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to submit changes: {ex.Message}");
                Debug.WriteLine($"Failed to submit changes: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task DeleteSelectedLocations()
        {
            if (IsBusy || SelectedItems.Count == 0)
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

                var idsToDelete = SelectedItems.Select(location => location.Id).ToList();

                var result = await _cardLocationService.DeleteLocationsAsync(idsToDelete);

                if (result.Result.Code is OperationResultCode.Success)
                {
                    foreach (int id in idsToDelete)
                    {
                        RemoveLocationFromCollection(id);
                    }

                    CollectionChanged?.Invoke(this, result.CollectionChangeSet);

                    IsDeleteConfirmationActive = false;
                    ResetEditorAndSelection();
                }

                ShowStatus(result.Result.Message);
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to delete selected locations: {ex.Message}");
                Debug.WriteLine($"Failed to delete selected locations: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }
        private void ReplaceLocationInCollection(CardLocation updatedLocation)
        {
            int index = Locations
                .Select((location, i) => new { location, i })
                .FirstOrDefault(x => x.location.Id == updatedLocation.Id)
                ?.i ?? -1;

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
    }
}
