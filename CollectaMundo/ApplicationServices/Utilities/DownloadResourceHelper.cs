using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace CollectaMundo.ApplicationServices.Utilities
{
    public static class DownloadResourceHelper
    {
        public static async Task<(bool success, string? errorMessage)> DownloadResourceAsync(string url, string targetPath, string taskLabel, Action<string>? onStart = null, Action<int>? onProgress = null, CancellationToken token = default)
        {
            Debug.WriteLine($"[DownloadResourceAsync] Preparing to download from {url} to {targetPath}");

            try
            {
                using var httpClient = new HttpClient();
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var buffer = new byte[8192];

                using var contentStream = await response.Content.ReadAsStreamAsync(token);
                using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                if (onStart != null && totalBytes > 0)
                {
                    onStart($"{totalBytes / 1_000_000.0:0.0} MB");
                }

                long totalBytesRead = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), token)) != 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), token);
                    totalBytesRead += bytesRead;

                    if (onProgress != null && totalBytes > 0)
                    {
                        onProgress((int)(100 * totalBytesRead / totalBytes));
                    }
                }

                Debug.WriteLine($"[DownloadResourceAsync] Download complete: {targetPath}");
                return (true, null);
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine($"[DownloadResourceAsync] Cancelled intentionally — suppress message");
                return (false, null); // Cancelled intentionally — suppress message
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DownloadResourceAsync] Error downloading {url}: {ex.Message}");
                return (false, $"{taskLabel} failed: {ex.Message}");
            }
        }
    }
}
