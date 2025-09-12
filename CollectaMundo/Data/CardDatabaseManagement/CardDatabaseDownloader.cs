using CollectaMundo.ApplicationServices.Utilities;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;

namespace CollectaMundo.Data.CardDatabaseManagement
{
    internal class CardDatabaseDownloader : ICardDatabaseDownloader
    {
        public async Task<OperationResult> DownloadAsync(string url, string targetPath, string label, int retryDelayInMs, IProgress<string> stepNameAndNumberProgress, IProgress<string> stepDetailAndErrorProgress, IProgress<int>? percentProgress = null, CancellationToken cancelToken = default)
        {
            return await RetryHelper.RetryLoopAsync(async () =>
                {
                    var (success, error) = await DownloadFileAsync(url, targetPath, label, stepDetailAndErrorProgress, percentProgress, cancelToken);
                    return success
                        ? new OperationResult(OperationResultCode.Success, $"{label} download succeeded.")
                        : new OperationResult(OperationResultCode.Error, error ?? $"{label} download failed.");
                },
                retryDelayInMs,
                stepName: label,
                stepNameAndNumberProgress,
                stepDetailAndErrorProgress,
                cancelToken: cancelToken);
        }
        public async Task<OperationResult> DownloadParallelAsync(
            string url1, string targetPath1, string label1,
            string url2, string targetPath2, string label2,
            int retryDelayInMs, string stepName, IProgress<string> stepNameAndNumberProgress, IProgress<string> stepDetailAndErrorProgress, IProgress<int>? percentProgress = null, CancellationToken cancelToken = default)
        {
            return await RetryHelper.RetryLoopAsync(
                async () =>
                {
                    var (success, errorMessage, cancelled) = await RunParallelDownloadsAsync(
                        url1, targetPath1, label1,
                        url2, targetPath2, label2,
                        stepDetailAndErrorProgress, percentProgress,
                        cancelToken);

                    if (cancelled)
                    {
                        return new OperationResult(OperationResultCode.CancelledByUser, "User cancelled download");
                    }

                    return success
                        ? new OperationResult(OperationResultCode.Success, "Parallel download succeeded.")
                        : new OperationResult(OperationResultCode.DownloadFailed, errorMessage ?? "Unknown download error");
                },
                retryDelayInMs,
                stepName,
                stepNameAndNumberProgress,
                stepDetailAndErrorProgress,
                cancelToken: cancelToken
            );
        }
        private static async Task<(bool success, string? errorMessage, bool cancelled)> RunParallelDownloadsAsync(
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
                // Cancel the other task
                innerCts.Cancel();

                // Await both safely without surfacing exceptions
                await SafeAwait(task1);
                await SafeAwait(task2);

                return (false, firstResult.errorMessage, token.IsCancellationRequested);
            }

            // Now wait for both normally, results already captured
            var result1 = await task1;
            var result2 = await task2;

            if (!result1.success || !result2.success)
            {
                var error = result1.errorMessage ?? result2.errorMessage ?? "Unknown error during parallel download.";
                return (false, error, token.IsCancellationRequested);
            }

            return (true, null, false);
        }

        private static async Task SafeAwait(Task<(bool success, string? errorMessage)> task)
        {
            try
            {
                await task;
            }
            catch
            {
                // Suppress any faulted or canceled task
            }
        }

        private static async Task<(bool success, string? errorMessage)> DownloadFileAsync(string url, string targetPath, string label, IProgress<string>? stepDetailAndErrorProgress, IProgress<int>? percentProgress, CancellationToken cancelToken)
        {
            try
            {
                Debug.WriteLine($"[Download] Starting download: {label} from {url} to {targetPath}");

                using var httpClient = new HttpClient();
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancelToken);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var buffer = new byte[8192];

                using var contentStream = await response.Content.ReadAsStreamAsync(cancelToken);
                using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                stepDetailAndErrorProgress?.Report($"{label} size: {totalBytes / 1_000_000.0:0.0} MB");

                long totalBytesRead = 0;
                int bytesRead;
                int lastReportedPercent = 0;

                while ((bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancelToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancelToken);
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
            catch (Exception ex) when (IsCancellation(ex, cancelToken))
            {
                Debug.WriteLine($"[Download] Cancelled: {label} — {ex.GetType().Name}: {ex.Message}");
                return (false, null); // Swallow cleanly
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Download] Error: {label}: {ex.Message}");
                return (false, $"{label} failed: {ex.Message}");
            }
        }
        private static bool IsCancellation(Exception ex, CancellationToken token)
        {
            return token.IsCancellationRequested
                || ex is OperationCanceledException
                || ex is TaskCanceledException
                || ex.InnerException is IOException ioEx && ioEx.InnerException is SocketException;
        }

    }
}

