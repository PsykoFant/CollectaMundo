using CollectaMundo.ApplicationServices.CardImages;
using CollectaMundo.ApplicationServices.CardImages.Models;
using CollectaMundo.DomainLogic.CardImages.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;
using System.Windows.Media.Imaging;

namespace CollectaMundo.ViewModels.SideMenuRight
{
    public partial class CardImageViewModel(ICardImageService cardImageService) : ObservableObject
    {
        private readonly ICardImageService _cardImageService = cardImageService;

        [ObservableProperty]
        private ICardImageSourceCard? selectedCard;

        partial void OnSelectedCardChanged(ICardImageSourceCard? value)
        {
            OnCardSelected(value);
        }
        private async void OnCardSelected(ICardImageSourceCard? selectedCard)
        {
            if (selectedCard is null)
            {
                ImagePromoType = string.Empty;
                ImageSet = string.Empty;
                FrontImageSource = null;
                BackImageSource = null;
                return;
            }

            var requestedCard = selectedCard;

            var imageResult = await _cardImageService.GetImageForCardAsync(
                new CardImageRequest
                {
                    Uuid = selectedCard.Uuid,
                    Name = selectedCard.Name,
                    Side = selectedCard.Side
                });

            if (!ReferenceEquals(SelectedCard, requestedCard))
            {
                return;
            }

            FrontImageSource = imageResult?.FrontImageBytes is not null
                ? LoadBitmapFromBytes(imageResult.FrontImageBytes)
                : null;

            BackImageSource = imageResult?.BackImageBytes is not null
                ? LoadBitmapFromBytes(imageResult.BackImageBytes)
                : null;

            ImageSet = !string.IsNullOrWhiteSpace(imageResult?.SetName)
                ? imageResult?.SetName
                : string.Empty;

            ImagePromoType = string.IsNullOrWhiteSpace(imageResult?.PromoType)
                ? null
                : imageResult.PromoType;
        }

        [ObservableProperty]
        private BitmapImage? frontImageSource;

        [ObservableProperty]
        private BitmapImage? backImageSource;

        [ObservableProperty]
        private string? imageSet;

        [ObservableProperty]
        private string? imagePromoType;
        private static BitmapImage LoadBitmapFromBytes(byte[] bytes)
        {
            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze(); // for thread safety
            return image;
        }

        public void ClearImages()
        {
            FrontImageSource = null;
            BackImageSource = null;
        }
    }
}


