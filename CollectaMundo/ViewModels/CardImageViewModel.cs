using CollectaMundo.ApplicationServices.CardImages;
using CollectaMundo.DomainLogic.CardLists.Models;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Media;

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

            var imageResult = await _cardImageService.GetImageForCardAsync(selectedCard);
            ImageSource = imageResult?.FrontImageSource;

        }

        private ImageSource? _imageSource;
        public ImageSource? ImageSource
        {
            get => _imageSource;
            set
            {
                if (_imageSource != value)
                {
                    _imageSource = value;
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

