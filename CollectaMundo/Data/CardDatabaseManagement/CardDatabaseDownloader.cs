using CollectaMundo.ApplicationServices.Utilities;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;

namespace CollectaMundo.Data.CardDatabaseManagement
{
    internal class CardDatabaseDownloader : ICardDatabaseDownloader
    {
        public async Task<OperationResult> DownloadAsync(
            string url,
            string targetPath,
            string label,
            int retryDelayInMs,
            IProgress<string> stepNameAndNumberProgress,
            IProgress<string> stepDetailAndErrorProgress,
            IProgress<int>? percentProgress = null,
            CancellationToken cancelToken = default)
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

            var userCancelled = false;

            try
            {
                using var httpClient = new HttpClient();
                using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var buffer = new byte[8192];

                using var contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true);

                stepDetailAndErrorProgress?.Report($"{label} size: {totalBytes / 1_000_000.0:0.0} MB");

                long totalBytesRead = 0;
                int lastReportedPercent = 0;

                while (true)
                {
                    // Don't pass cancellation token here
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

                    // Poll for cancellation
                    if (cancelToken.IsCancellationRequested)
                    {
                        userCancelled = true;
                        break; // cleanly exit the read loop — let response + stream dispose safely
                    }
                }

                Debug.WriteLine($"[Download] {(userCancelled ? "Cancelled safely" : "Completed")}: {label}");
                return (userCancelled ? (false, null, true) : (true, null, false));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Download] Error: {label}: {ex}");
                return (false, $"{label} failed: {ex.Message}", false);
            }
        }

        private static bool IsCancellation(Exception ex, CancellationToken token)
        {
            return token.IsCancellationRequested
                || ex is OperationCanceledException
                || ex is TaskCanceledException
                // The real-world case you’re hitting:
                || ex is IOException ioEx && ioEx.InnerException is SocketException;
        }


    }
}

