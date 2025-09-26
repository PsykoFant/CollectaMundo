using CollectaMundo.ApplicationServices.CardImages;
using CollectaMundo.DomainLogic.CardLists.Models;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

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
                FrontImageSource = null;
                return;
            }

            var imageResult = await _cardImageService.GetImageForCardAsync(selectedCard);
            //FrontImageSource = ConvertToImageSource(imageResult?.FrontImageUrl);
            //FrontImageSource = await DownloadImageAsync("https://cards.scryfall.io/normal/front/x/x/doesnotexist.jpg");
            FrontImageSource = await DownloadImageAsync(imageResult?.FrontImageUrl);
            BackImageSource = await DownloadImageAsync(imageResult?.BackImageUrl);
        }

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

        // Helper method to convert URL string to BitmapImage
        private static async Task<BitmapImage?> DownloadImageAsync(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            try
            {
                using var httpClient = new HttpClient();
                var imageBytes = await httpClient.GetByteArrayAsync(url);

                using var stream = new MemoryStream(imageBytes);

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze(); // Now safe to freeze — all data is loaded

                return bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Image load failed: {ex.Message}");
                return null;
            }
        }




        //private static BitmapImage? ConvertToImageSource(string? url)
        //{
        //    try
        //    {
        //        if (string.IsNullOrWhiteSpace(url)) return null;
        //        //var uri = new Uri(url, UriKind.Absolute);
        //        //var uri = new Uri("bogus url", UriKind.Absolute);                
        //        var uri = new Uri("https://cards.scryfall.io/normal/back/8/2/829d91e9-4878-4e55-a262-ac0d55b65d4e.jpg", UriKind.Absolute); // url gives 404

        //        return new BitmapImage(uri);
        //    }
        //    catch
        //    {
        //        return null;
        //    }
        //}
    }
}

