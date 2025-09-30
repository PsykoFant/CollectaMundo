using CollectaMundo.ApplicationServices.Shared;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows.Media.Imaging;

namespace CollectaMundo.Infrastructure.CardImages
{
    public class CardImageDownloader(IAppSettings settings) : ICardImageDownloader
    {
        private readonly HttpClient _httpClient = new();
        private readonly IAppSettings _settings = settings;

        public async Task<BitmapImage?> DownloadAsync(string? url)
        {

            Debug.WriteLine($"Diskpath for image cache: {_settings.CardImageCachePath}");

            try
            {
                if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    Debug.WriteLine("Invalid or null URL provided.");
                    return null;
                }

                var imageBytes = await _httpClient.GetByteArrayAsync(url);
                using var stream = new MemoryStream(imageBytes);

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze(); // safely cross-thread usable

                return bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Image load failed: {ex.Message}");
                Debug.WriteLine($"Failing url: {url}");
                return null; // fail silently on 404, etc.
            }
        }
    }

}
