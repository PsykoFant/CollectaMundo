using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows.Media.Imaging;

namespace CollectaMundo.Infrastructure.CardImages
{
    public class CardImageDownloader : ICardImageDownloader
    {
        private readonly HttpClient _httpClient = new();

        public async Task<BitmapImage?> DownloadAsync(string? url)
        {
            try
            {
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
                return null; // fail silently on 404, etc.
            }
        }
    }

}
