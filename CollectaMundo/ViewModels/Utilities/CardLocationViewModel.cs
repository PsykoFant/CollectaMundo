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
        private bool isBusy;

        [ObservableProperty]
        private string pageTitle = "Manage Locations";
        public ObservableCollection<CardLocation> Locations { get; } = [];
        public async Task LoadCardLocationsAsync()
        {
            if (IsBusy)
            {
                return;
            }

            await LoadInternalAsync();
        }

        [ObservableProperty]
        private string locationName = string.Empty;

        [ObservableProperty]
        private CardLocationType selectedLocationType = CardLocationType.Storage;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        [ObservableProperty]
        private bool isStatusVisible;

        public ObservableCollection<CardLocationType> LocationTypes { get; } =
        [
            CardLocationType.Storage,
            CardLocationType.Deck
        ];

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

                var mutation = await _cardLocationService.CreateAsync(LocationName, SelectedLocationType);
                StatusMessage = mutation.Result.Message;

                IsStatusVisible = !string.IsNullOrWhiteSpace(mutation.Result.Message);

                if (mutation.Result.Code == OperationResultCode.Success && mutation.Location is not null)
                {
                    Locations.Add(mutation.Location);
                }
            }
            finally
            {
                IsBusy = false;
            }
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
    }
}
