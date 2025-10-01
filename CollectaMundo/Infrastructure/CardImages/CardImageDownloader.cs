using CollectaMundo.ApplicationServices.Shared;
using System.Diagnostics;
using System.Net.Http;

namespace CollectaMundo.Infrastructure.CardImages
{
    public class CardImageDownloader(IAppSettings settings) : ICardImageDownloader
    {
        private readonly HttpClient _httpClient = new();
        private readonly IAppSettings _settings = settings;

        public async Task<byte[]?> DownloadAsync(string? url)
        {
            Debug.WriteLine($"Diskpath for image cache: {_settings.CardImageCachePath}");

            try
            {
                if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    Debug.WriteLine("Invalid or null URL provided.");
                    return null;
                }

                // Download the raw bytes directly
                return await _httpClient.GetByteArrayAsync(url);
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
