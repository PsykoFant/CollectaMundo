using CollectaMundo.ApplicationServices.Shared;
using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace CollectaMundo.Infrastructure.CardImages
{
    public class CardImageDownloader(IAppSettings settings) : ICardImageDownloader
    {
        private readonly HttpClient _httpClient = new();
        private readonly IAppSettings _settings = settings;

        private const string DefaultSize = "normal";
        private const string Extension = ".jpg";

        public async Task<byte[]?> DownloadAsync(string? url, string uuid, string side)
        {
            var cacheDir = _settings.CardImageCachePath;
            var fileName = $"{uuid}_{side}_{DefaultSize}{Extension}";
            var filePath = Path.Combine(cacheDir, fileName);

            // 1. Try to load from disk
            if (File.Exists(filePath))
            {
                try
                {
                    return await File.ReadAllBytesAsync(filePath).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ImageDownloader] Failed to read cached image: {ex.Message}");
                    // Continue to try downloading
                }
            }

            // 2. Fallback to HTTP
            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                Debug.WriteLine("[ImageDownloader] Invalid or null URL provided.");
                return null;
            }

            try
            {
                var bytes = await _httpClient.GetByteArrayAsync(uri).ConfigureAwait(false);

                // Save downloaded image to cache
                try
                {
                    Directory.CreateDirectory(cacheDir);
                    await File.WriteAllBytesAsync(filePath, bytes).ConfigureAwait(false);
                    Debug.WriteLine($"[ImageDownloader] Image saved to cache: {filePath}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ImageDownloader] Failed to save image to cache: {ex.Message}");
                }

                return bytes;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ImageDownloader] HTTP download failed: {ex.Message}");
                Debug.WriteLine($"[ImageDownloader] URL: {url}");
                return null;
            }
        }
    }

}
