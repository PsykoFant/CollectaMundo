using CollectaMundo.ApplicationServices.CardImages;
using CollectaMundo.DomainLogic.CardLists.Models;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace CollectaMundo.ViewModels
{
    public class CardImageViewModel(ICardImageService cardImageService) : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        private readonly ICardImageService _cardImageService = cardImageService;
        private CardSet? _selectedCard;
        public CardSet? SelectedCard
        {
            get => _selectedCard;
            set
            {
                if (_selectedCard != value)
                {
                    _selectedCard = value;
                    OnPropertyChanged();
                    OnCardSelected(_selectedCard); // Notify image view model
                }
            }
        }

        private async void OnCardSelected(CardSet? selectedCard)
        {
            if (selectedCard is null)
            {
                Debug.WriteLine("No card selected.");
                return;
            }

            var imageResult = await _cardImageService.GetImageForCardAsync(selectedCard.Uuid, selectedCard.Name);

            string frontImageUrl = imageResult?.FrontImageUrl ?? string.Empty;

            ImageSourceUrl = frontImageUrl;
        }


        private string? _imageSourceUrl = string.Empty;
        public string? ImageSourceUrl
        {
            get => _imageSourceUrl;
            set
            {
                if (_imageSourceUrl != value)
                {
                    _imageSourceUrl = value;
                    OnPropertyChanged();
                }
            }
        }

        private string? _imageSourceUrl2nd = string.Empty;
        public string? ImageSourceUrl2nd
        {
            get => _imageSourceUrl2nd;
            set
            {
                if (_imageSourceUrl2nd != value)
                {
                    _imageSourceUrl2nd = value;
                    OnPropertyChanged(nameof(ImageSourceUrl2nd));
                }
            }
        }

    }
}

