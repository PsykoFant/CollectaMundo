using CollectaMundo.ApplicationServices.CardLocations;
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

        public ObservableCollection<CardLocationType> LocationTypes { get; } =
        [
            CardLocationType.Storage,
            CardLocationType.Deck
        ];

        [RelayCommand]
        private async Task SubmitLocation()
        {
            var result = await _cardLocationService.CreateAsync(LocationName, SelectedLocationType);

            StatusMessage = result.Message;
            IsStatusVisible = !string.IsNullOrWhiteSpace(result.Message);

            if (result.Code == ApplicationServices.Shared.OperationResultCode.Success)
            {
                LocationName = string.Empty;
                SelectedLocationType = CardLocationType.Storage;
            }
        }
    }
}
