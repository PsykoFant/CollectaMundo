using CollectaMundo.ApplicationServices.Utilities;
using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace CollectaMundo.ApplicationServices.DownloadResourceFiles
{
    public class DownloadService : IDownloadService
    {
        public async Task<OperationResult> DownloadAsync(string url, string targetPath, string label, int retryDelayInMs, IProgress<string> stepNameAndNumberProgress, IProgress<string> stepDetailAndErrorProgress, IProgress<int>? percentProgress = null, CancellationToken token = default)
        {
            return await RetryHelper.RetryLoopAsync(async () =>
                {
                    var (success, error) = await DownloadFileAsync(url, targetPath, label, stepDetailAndErrorProgress, percentProgress, token);
                    return success
                        ? new OperationResult(OperationResultCode.Success, $"{label} download succeeded.")
                        : new OperationResult(OperationResultCode.Error, error ?? $"{label} download failed.");
                },
                retryDelayInMs,
                stepName: label,
                stepNameAndNumberProgress,
                stepDetailAndErrorProgress
            );
        }
        public async Task<OperationResult> DownloadParallelAsync(
            string url1, string targetPath1, string label1,
            string url2, string targetPath2, string label2,
            int retryDelayInMs, string stepName, IProgress<string> stepNameAndNumberProgress, IProgress<string> stepDetailAndErrorProgress, IProgress<int>? percentProgress = null, CancellationToken token = default)
        {
            return await RetryHelper.RetryLoopAsync(
                async () =>
                {
                    var (success, errorMessage) = await RunParallelDownloadsAsync(
                        url1, targetPath1, label1,
                        url2, targetPath2, label2,
                        stepDetailAndErrorProgress, percentProgress, token);

                    return success
                        ? new OperationResult(OperationResultCode.Success, "Parallel download succeeded.")
                        : new OperationResult(OperationResultCode.DownloadFailed, errorMessage ?? "Unknown download error");
                },
                retryDelayInMs,
                stepName,
                stepNameAndNumberProgress,
                stepDetailAndErrorProgress
            );
        }

        private static async Task<(bool success, string? errorMessage)> RunParallelDownloadsAsync(
            string url1, string targetPath1, string label1,
            string url2, string targetPath2, string label2,
            IProgress<string>? stepDetailAndErrorProgress, IProgress<int>? percentProgress, CancellationToken token)
        {
            using var innerCts = new CancellationTokenSource();
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(innerCts.Token, token);
            var linkedToken = linkedCts.Token;

            var task1 = DownloadFileAsync(url1, targetPath1, label1, stepDetailAndErrorProgress, percentProgress, linkedToken);
            var task2 = DownloadFileAsync(url2, targetPath2, label2, null, null, linkedToken);

            var firstCompleted = await Task.WhenAny(task1, task2);
            var firstResult = await firstCompleted;

            if (!firstResult.success)
            {
                innerCts.Cancel(); // cancel the other download
                await Task.WhenAll(task1, task2); // ensure cleanup
                return firstResult;
            }

            var result1 = await task1;
            var result2 = await task2;

            if (!result1.success || !result2.success)
            {
                var error = result1.errorMessage ?? result2.errorMessage ?? "Unknown error during parallel download.";
                return (false, error);
            }

            return (true, null);
        }
        private static async Task<(bool success, string? errorMessage)> DownloadFileAsync(string url, string targetPath, string label, IProgress<string>? stepDetailAndErrorProgress, IProgress<int>? percentProgress, CancellationToken token)
        {
            try
            {
                Debug.WriteLine($"[Download] Starting download: {label} from {url} to {targetPath}");

                using var httpClient = new HttpClient();
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var buffer = new byte[8192];

                using var contentStream = await response.Content.ReadAsStreamAsync(token);
                using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                stepDetailAndErrorProgress?.Report($"{label} size: {totalBytes / 1_000_000.0:0.0} MB");

                long totalBytesRead = 0;
                int bytesRead;
                int lastReportedPercent = 0;

                while ((bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), token)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), token);
                    totalBytesRead += bytesRead;

                    if (totalBytes > 0 && percentProgress != null)
                    {
                        int percent = (int)((double)totalBytesRead / totalBytes * 100);
                        if (percent > lastReportedPercent)
                        {
                            lastReportedPercent = percent;
                            percentProgress.Report(percent);
                        }
                    }
                }

                Debug.WriteLine($"[Download] Completed: {label}");
                return (true, null);
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine($"[Download] Cancelled: {label}");
                return (false, null); // Suppressed
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Download] Error: {label}: {ex.Message}");
                return (false, $"{label} failed: {ex.Message}");
            }
        }
    }
}

