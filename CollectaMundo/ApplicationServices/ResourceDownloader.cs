using CollectaMundo.ViewModels;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;

namespace CollectaMundo.ApplicationServices
{
    public class ResourceDownloader : IResourceDownloader
    {
        public async Task<bool> DownloadAsync(string url, string targetPath, string description, bool showProgress, StatusViewModel statusVm)
        {
            try
            {
                using var httpClient = new HttpClient();
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var totalBytesRead = 0L;
                var buffer = new byte[8192];
                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var megabytes = string.Format("{0:0.0} MB", totalBytes / 1_000_000.0);
                statusVm.Show($"Downloading {description} ({megabytes})", showProgress);

                int bytesRead;
                while ((bytesRead = await contentStream.ReadAsync(buffer)) != 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    totalBytesRead += bytesRead;

                    if (showProgress && totalBytes > 0)
                    {
                        double percent = (double)totalBytesRead / totalBytes * 100;
                        Debug.WriteLine($"Progress: {percent:0.0}%");
                    }
                }

                Debug.WriteLine($"Download complete: {targetPath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Download error: {ex.Message}");
                MessageBox.Show($"Error during download: {ex.Message}", "Download Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
    }

}
