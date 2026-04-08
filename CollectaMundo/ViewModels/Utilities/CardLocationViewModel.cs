using CollectaMundo.ApplicationServices.CardLocations;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardLocations.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace CollectaMundo.ViewModels.Utilities
{
    public partial class CardLocationViewModel(ICardLocationService cardLocationService) : ObservableObject
    {
        private readonly ICardLocationService _cardLocationService = cardLocationService;

        [ObservableProperty]
        private string pageTitle = "Manage Locations";

        [ObservableProperty]
        private string locationName = string.Empty;

        [ObservableProperty]
        private CardLocationType selectedLocationType = CardLocationType.Storage;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        [ObservableProperty]
        private bool isStatusVisible;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private CardLocation? selectedLocation;
        public ObservableCollection<CardLocation> Locations { get; } = [];
        public ObservableCollection<CardLocationType> LocationTypes { get; } = [CardLocationType.Storage,CardLocationType.Deck];
        public bool IsEditing => SelectedLocation is not null;
        public string SubmitButtonText => IsEditing ? "Save changes" : "Add location";
        partial void OnSelectedLocationChanged(CardLocation? value)
        {
            if (value is not null)
            {
                LocationName = value.Name;
                SelectedLocationType = value.Type;
            }

            OnPropertyChanged(nameof(IsEditing));
            OnPropertyChanged(nameof(SubmitButtonText));
        }
        private void ExitEditModeAndClearEditor()
        {
            SelectedLocation = null;
            LocationName = string.Empty;
            SelectedLocationType = CardLocationType.Storage;
        }

        // Public method to load locations, can be called from outside (e.g., when the view appears)
        public async Task LoadCardLocationsAsync()
        {
            if (IsBusy)
                return;

            await LoadInternalAsync();
        }
        private async Task LoadInternalAsync()
        {
            try
            {
                IsBusy = true;

                var locations = await _cardLocationService.GetAllAsync();

                Locations.Clear();

                foreach (var loc in locations)
                {
                    Locations.Add(loc);
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        // Command to handle both adding and updating locations

        [RelayCommand]
        private async Task SubmitLocation()
        {
            if (IsBusy)
                return;

            try
            {
                IsBusy = true;
                IsStatusVisible = false;
                StatusMessage = string.Empty;

                if (IsEditing && SelectedLocation is not null)
                {
                    var mutation = await _cardLocationService.UpdateAsync(
                        SelectedLocation.Id,
                        LocationName,
                        SelectedLocationType);

                    StatusMessage = mutation.Result.Message;
                    IsStatusVisible = !string.IsNullOrWhiteSpace(StatusMessage);

                    if (mutation.Result.Code == OperationResultCode.Success && mutation.Location is not null)
                    {
                        ReplaceLocationInCollection(mutation.Location);
                        ExitEditModeAndClearEditor();
                    }
                }
                else
                {
                    var mutation = await _cardLocationService.CreateAsync(LocationName, SelectedLocationType);

                    StatusMessage = mutation.Result.Message;
                    IsStatusVisible = !string.IsNullOrWhiteSpace(StatusMessage);

                    if (mutation.Result.Code == OperationResultCode.Success && mutation.Location is not null)
                    {
                        Locations.Add(mutation.Location);
                        ExitEditModeAndClearEditor();
                    }
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
            ExitEditModeAndClearEditor();
            IsStatusVisible = false;
            StatusMessage = string.Empty;
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
    }
}
