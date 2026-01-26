using CollectaMundo.ApplicationServices.CardImages;
using CollectaMundo.DomainLogic.CardLists.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;
using System.Windows.Media.Imaging;

namespace CollectaMundo.ViewModels
{
    public partial class CardImageViewModel(ICardImageService cardImageService) : ObservableObject
    {
        private readonly ICardImageService _cardImageService = cardImageService;

        [ObservableProperty]
        private CardSet? selectedCard;

        partial void OnSelectedCardChanged(CardSet? value)
        {
            OnCardSelected(value);
        }
        private async void OnCardSelected(CardSet? selectedCard)
        {
            if (selectedCard is null)
            {
                ImagePromoType = string.Empty;
                ImageSet = string.Empty;
                FrontImageSource = null;
                BackImageSource = null;
                return;
            }

            var imageResult = await _cardImageService.GetImageForCardAsync(selectedCard);

            FrontImageSource = imageResult?.FrontImageBytes is not null
                ? LoadBitmapFromBytes(imageResult.FrontImageBytes)
                : null;

            BackImageSource = imageResult?.BackImageBytes is not null
                ? LoadBitmapFromBytes(imageResult.BackImageBytes)
                : null;

            ImageSet = !string.IsNullOrWhiteSpace(selectedCard.SetName)
                ? selectedCard.SetName
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
    }
}


