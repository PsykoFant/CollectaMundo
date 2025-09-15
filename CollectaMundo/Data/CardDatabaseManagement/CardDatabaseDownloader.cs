using CollectaMundo.ApplicationServices.Utilities;
using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace CollectaMundo.Data.CardDatabaseManagement
{
    internal class CardDatabaseDownloader : ICardDatabaseDownloader
    {
        public async Task<OperationResult> DownloadAsync(string url, string targetPath, string label, int retryDelayInMs, IProgress<string> stepNameAndNumberProgress, IProgress<string> stepDetailAndErrorProgress, IProgress<int>? percentProgress = null, CancellationToken cancelToken = default)
        {
            return await RetryHelper.RetryLoopAsync(async () =>
            {
                var (success, error, cancelled) = await DownloadFileAsync(
                    url, targetPath, label,
                    stepDetailAndErrorProgress,
                    percentProgress,
                    cancelToken);

                if (cancelled)
                    return new OperationResult(OperationResultCode.CancelledByUser, "User cancelled download");

                return success
                    ? new OperationResult(OperationResultCode.Success, $"{label} download succeeded.")
                    : new OperationResult(OperationResultCode.Error, error ?? $"{label} download failed."); // <== message from HTTP error is preserved
            },
            retryDelayInMs, stepName: label, stepNameAndNumberProgress, stepDetailAndErrorProgress, cancelToken: cancelToken);
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
                //await SafeAwait(task1);
                //await SafeAwait(task2);

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


        private static async Task<(bool success, string? errorMessage, bool cancelled)> DownloadFileAsync(
    string url,
    string targetPath,
    string label,
    IProgress<string>? stepDetailAndErrorProgress,
    IProgress<int>? percentProgress,
    CancellationToken cancelToken)
        {
            Debug.WriteLine($"[Download] Starting download: {label} from {url} to {targetPath}");

            bool shouldDeletePartialFile = false;

            try
            {
                using var httpClient = new HttpClient();

                Debug.WriteLine($"[Download] Sending GET request for: {url}");
                var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

                Debug.WriteLine($"[Download] Response received: {(int)response.StatusCode} {response.ReasonPhrase}");

                // Explicit check instead of EnsureSuccessStatusCode, to log properly
                if (!response.IsSuccessStatusCode)
                {
                    var msg = $"HTTP error: {(int)response.StatusCode} {response.ReasonPhrase}";
                    Debug.WriteLine($"[Download] {msg}");
                    return (false, msg, false); // <-- bubble to user after retries
                }

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var buffer = new byte[8192];

                Debug.WriteLine("[Download] Opening response stream");
                using var contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

                Debug.WriteLine($"[Download] Creating file stream at: {targetPath}");
                using var fileStream = new FileStream(
                    targetPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 8192,
                    useAsync: true);

                stepDetailAndErrorProgress?.Report($"{label} size: {totalBytes / 1_000_000.0:0.0} MB");

                long totalBytesRead = 0;
                int lastReportedPercent = 0;

                while (true)
                {
                    var bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length)).ConfigureAwait(false);
                    if (bytesRead == 0) break;

                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead)).ConfigureAwait(false);
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

                    if (cancelToken.IsCancellationRequested)
                    {
                        Debug.WriteLine($"[Download] Cancellation requested during read: {label}");
                        shouldDeletePartialFile = true;
                        return (false, null, true);
                    }
                }

                Debug.WriteLine($"[Download] Completed successfully: {label}");
                return (true, null, false);
            }
            catch (Exception ex)
            {
                shouldDeletePartialFile = true;
                Debug.WriteLine($"[Download] EXCEPTION during download of {label}: {ex.GetType().Name}: {ex.Message}");
                return (false, $"{label} failed: {ex.Message}", false);
            }
            finally
            {
                if (shouldDeletePartialFile)
                {
                    CleanupPartialDownload(targetPath);
                }
            }
        }




        private static void CleanupPartialDownload(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Debug.WriteLine($"[Cleanup] Deleted partial file: {filePath}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Cleanup] Failed to delete {filePath}: {ex.Message}");
            }
        }
    }
}

