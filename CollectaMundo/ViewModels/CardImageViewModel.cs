using CollectaMundo.ApplicationServices.CardImages;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.Infrastructure.CardImages;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace CollectaMundo.ViewModels
{
    public class CardImageViewModel(ICardImageService cardImageService, ICardImageDownloader cardImageDownloader) : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        private readonly ICardImageService _cardImageService = cardImageService;
        private readonly ICardImageDownloader _cardImageDownloader = cardImageDownloader;
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
                FrontImageSource = null;
                return;
            }

            var imageResult = await _cardImageService.GetImageForCardAsync(selectedCard);
            FrontImageSource = await _cardImageDownloader.DownloadAsync("https://cards.scryfall.io/normal/front/x/x/doesnotexist.jpg");
            //FrontImageSource = await _cardImageDownloader.DownloadAsync("bogus");

            //FrontImageSource = await _cardImageDownloader.DownloadAsync(imageResult?.FrontImageUrl);
            BackImageSource = string.IsNullOrWhiteSpace(imageResult?.BackImageUrl) ? null : await _cardImageDownloader.DownloadAsync(imageResult.BackImageUrl);

        }

        // Helper method to convert URL string to BitmapImage
        //private static async Task<BitmapImage?> DownloadImageAsync(string? url)
        //{
        //    if (string.IsNullOrWhiteSpace(url))
        //    {
        //        return null;
        //    }

        //    try
        //    {
        //        using var httpClient = new HttpClient();
        //        var imageBytes = await httpClient.GetByteArrayAsync(url);

        //        using var stream = new MemoryStream(imageBytes);

        //        var bitmap = new BitmapImage();
        //        bitmap.BeginInit();
        //        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        //        bitmap.StreamSource = stream;
        //        bitmap.EndInit();
        //        bitmap.Freeze();

        //        return bitmap;
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.WriteLine($"Image load failed: {ex.Message}");
        //        return null;
        //    }
        //}

        private BitmapImage? _frontImageSource;
        public BitmapImage? FrontImageSource
        {
            get => _frontImageSource;
            set
            {
                if (_frontImageSource != value)
                {
                    _frontImageSource = value;
                    OnPropertyChanged();
                }
            }
        }

        private BitmapImage? _backImageSource;
        public BitmapImage? BackImageSource
        {
            get => _backImageSource;
            set
            {
                if (_backImageSource != value)
                {
                    _backImageSource = value;
                    OnPropertyChanged();
                }
            }
        }


    }
}


