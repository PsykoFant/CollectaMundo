using CollectaMundo.ApplicationServices.CardImages;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.Infrastructure.CardImages;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media.Imaging;

namespace CollectaMundo.ViewModels
{
    public partial class CardImageViewModel(ICardImageService cardImageService, ICardImageDownloader cardImageDownloader) : ObservableObject
    {
        private readonly ICardImageService _cardImageService = cardImageService;
        private readonly ICardImageDownloader _cardImageDownloader = cardImageDownloader;

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
                FrontImageSource = null;
                BackImageSource = null;
                return;
            }

            var imageResult = await _cardImageService.GetImageForCardAsync(selectedCard);
            FrontImageSource = string.IsNullOrWhiteSpace(imageResult?.FrontImageUrl) ? null : await _cardImageDownloader.DownloadAsync(imageResult.FrontImageUrl);
            BackImageSource = string.IsNullOrWhiteSpace(imageResult?.BackImageUrl) ? null : await _cardImageDownloader.DownloadAsync(imageResult.BackImageUrl);
            ImageSet = !string.IsNullOrWhiteSpace(selectedCard.SetName) ? selectedCard.SetName : String.Empty;
            ImagePromotType = string.IsNullOrWhiteSpace(imageResult?.PromoType) ? null : imageResult.PromoType;
        }


        [ObservableProperty]
        private BitmapImage? frontImageSource;

        [ObservableProperty]
        private BitmapImage? backImageSource;

        [ObservableProperty]
        private string? imageSet;

        [ObservableProperty]
        private string? imagePromotType;
    }
}


