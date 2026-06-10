using CollectaMundo.ApplicationServices.CardLocations;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.CardLocations.Models;
using CollectaMundo.DomainLogic.Shared.Models;
using CollectaMundo.ViewModels.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace CollectaMundo.ViewModels.Utilities
{
    public partial class CardLocationViewModel(ICardLocationService cardLocationService) : LocationManagementViewModel<CardLocation>
    {
        private readonly ICardLocationService _cardLocationService = cardLocationService;

        // UI text
        protected override LocationManagementText Text { get; } = new(
            CreateText: "Add location",
            EditText: "Edit location",
            SaveText: "Save changes",
            BulkUpdateText: "Update selected",
            CreateMode: "Add a new card location",
            SelectedReadOnlyMode: string.Empty,
            EditSingleMode: "Edit selected card location",
            EditMultipleMode: "Edit selected card locations");

        [ObservableProperty]
        private string locationName = string.Empty;

        [ObservableProperty]
        private CardLocationType? selectedLocationType;

        // View data
        public ObservableCollection<CardLocation> Locations { get; } = [];
        public ObservableCollection<CardLocationType> LocationTypes { get; } =
        [
            CardLocationType.Storage,
            CardLocationType.Deck
        ];

        // Editor state hooks
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
            SelectedLocationType = CardLocationType.Storage;
        }

        // CRUD operations
        protected override async Task CreateAsync()
        {
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
        protected override async Task UpdateSingleAsync(CardLocation selectedLocation)
        {
            if (SelectedLocationType is not CardLocationType locationType)
            {
                ShowStatus("Select a location type before saving changes.");
                return;
            }

            var mutation = await _cardLocationService.UpdateLocationAsync(selectedLocation.Id, LocationName, locationType);

            if (mutation.Result.Code is OperationResultCode.Success &&
                mutation.Entity is not null)
            {
                UpdateLocationInCollection(mutation.Entity);
                ResetEditorAndSelection();
            }

            ShowStatus(mutation.Result.Message);
        }
        protected override async Task UpdateMultipleAsync(IReadOnlyList<CardLocation> selectedLocations)
        {
            if (SelectedLocationType is not CardLocationType locationType)
            {
                ShowStatus("Select a location type before updating selected locations.");
                return;
            }

            var ids = selectedLocations.Select(location => location.Id).ToList();

            var updatedLocations = await _cardLocationService.UpdateLocationTypesAsync(ids, locationType);

            foreach (var updatedLocation in updatedLocations)
            {
                UpdateLocationInCollection(updatedLocation);
            }

            ResetEditorAndSelection();

            ShowStatus(updatedLocations.Count == 1
                ? "Location updated successfully."
                : $"{updatedLocations.Count} locations updated successfully.");
        }

        // Data loading
        public Task LoadCardLocationsAsync()
        {
            return RunBusyOperationAsync(async () =>
            {
                var loadedLocations = (await _cardLocationService.GetAllLocationsAsync()).ToList();

                Locations.Clear();

                foreach (var location in loadedLocations)
                {
                    Locations.Add(location);
                }
            },
            "Failed to load card locations");
        }

        // External notifications
        public event EventHandler<CollectionChangeSet<CardSet>>? CollectionChanged;

        // Commands
        [RelayCommand]
        private Task DeleteSelectedLocations()
        {
            return DeleteSelectedItemsAsync(
                "This will also delete the location from cards with that location in your collection (if any).",
                async selectedLocations =>
                {
                    var idsToDelete = selectedLocations.Select(location => location.Id).ToList();
                    var entityName = idsToDelete.Count == 1 ? "location" : "locations";

                    var result = await _cardLocationService.DeleteLocationsAsync(idsToDelete, entityName);

                    if (result.Result.Code is OperationResultCode.Success)
                    {
                        foreach (int id in idsToDelete)
                        {
                            RemoveLocationFromCollection(id);
                        }

                        CollectionChanged?.Invoke(this, result.CollectionChangeSet);
                    }

                    ShowStatus(result.Result.Message);

                    return result.Result.Code is OperationResultCode.Success;
                });
        }

        // Collection synchronization
        private void UpdateLocationInCollection(CardLocation updatedLocation)
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
