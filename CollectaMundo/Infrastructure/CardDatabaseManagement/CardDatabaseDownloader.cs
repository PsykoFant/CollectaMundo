using CollectaMundo.ApplicationServices.Shared;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using HttpClient = System.Net.Http.HttpClient;

namespace CollectaMundo.Infrastructure.CardDatabaseManagement
{
    public class CardDatabaseDownloader(HttpClient? httpClient = null) : ICardDatabaseDownloader
    {
        private readonly HttpClient _httpClient = httpClient ?? new HttpClient();

        public async Task<OperationResult> DownloadAsync(string url, string targetPath, string label, int retryDelayInMs, IProgress<string> stepNameAndNumberProgress, IProgress<string> stepDetailAndErrorProgress, IProgress<int>? percentProgress = null, CancellationToken cancelToken = default)
        {
            return await RetryHelper.RetryLoopAsync(async () =>
            {
                var (success, error, cancelled) = await DownloadFileAsync(
                    url, targetPath, label,
                    stepDetailAndErrorProgress,
                    percentProgress,
                    _httpClient,
                    cancelToken);

                if (cancelled)
                {
                    return new OperationResult(OperationResultCode.CancelledByUser, "User cancelled download");
                }

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
                    using var innerCts = new CancellationTokenSource();
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(innerCts.Token, cancelToken);
                    var linkedToken = linkedCts.Token;

                    var task1 = DownloadFileAsync(url1, targetPath1, label1, stepDetailAndErrorProgress, percentProgress, _httpClient, linkedToken);
                    var task2 = DownloadFileAsync(url2, targetPath2, label2, null, null, _httpClient, linkedToken);

                    try
                    {
                        var firstCompleted = await Task.WhenAny(task1, task2);
                        var firstResult = await firstCompleted;

                        if (!firstResult.success)
                        {
                            // Cancel the other task
                            innerCts.Cancel();

                            return firstResult.cancelled
                                ? new OperationResult(OperationResultCode.CancelledByUser, "User cancelled download")
                                : new OperationResult(OperationResultCode.DownloadFailed, firstResult.errorMessage ?? "Unknown error");
                        }

                        (bool success2, string? error2, bool cancelled2) = (false, null, false);

                        try
                        {
                            if (task1 != firstCompleted)
                                (success2, error2, cancelled2) = await task1;
                            else
                                (success2, error2, cancelled2) = await task2;
                        }
                        catch (OperationCanceledException)
                        {
                            return new OperationResult(OperationResultCode.CancelledByUser, "User cancelled second task during parallel download.");
                        }
                        catch (ObjectDisposedException ex) when (linkedToken.IsCancellationRequested)
                        {
                            Debug.WriteLine($"[ParallelDownload] Safe ObjectDisposedException on second task: {ex.Message}");
                            return new OperationResult(OperationResultCode.CancelledByUser, "User cancelled during parallel download (stream disposed).");
                        }
                        catch (IOException ex) when (linkedToken.IsCancellationRequested)
                        {
                            Debug.WriteLine($"[ParallelDownload] Safe IOException on second task: {ex.Message}");
                            return new OperationResult(OperationResultCode.CancelledByUser, "User cancelled during parallel download (stream IO).");
                        }

                        if (!success2)
                        {
                            var error = error2 ?? "Unknown error during second download.";
                            return new OperationResult(OperationResultCode.DownloadFailed, error);
                        }

                        return new OperationResult(OperationResultCode.Success, "Parallel download succeeded.");
                    }
                    catch (OperationCanceledException)
                    {
                        return new OperationResult(OperationResultCode.CancelledByUser, "User cancelled during parallel download.");
                    }
                }, retryDelayInMs, stepName, stepNameAndNumberProgress, stepDetailAndErrorProgress, cancelToken: cancelToken);
        }

        private static async Task<(bool success, string? errorMessage, bool cancelled)> DownloadFileAsync(string url, string targetPath, string label, IProgress<string>? stepDetailAndErrorProgress, IProgress<int>? percentProgress, HttpClient httpClient, CancellationToken cancelToken)
        {
            Debug.WriteLine($"[Download] Starting download: {label} from {url} to {targetPath}");

            bool shouldDeletePartialFile = false;

            try
            {
                Debug.WriteLine($"[Download] Sending GET request for: {url}");
                var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancelToken)
                                               .ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var msg = $"HTTP error: {(int)response.StatusCode} {response.ReasonPhrase}";
                    Debug.WriteLine($"[Download] {msg}");
                    return (false, msg, false);
                }

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var buffer = new byte[8192];

                using var contentStream = await response.Content.ReadAsStreamAsync(cancelToken).ConfigureAwait(false);
                using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                stepDetailAndErrorProgress?.Report($"{label} size: {totalBytes / 1_000_000.0:0.0} MB");

                long totalBytesRead = 0;
                int lastReportedPercent = 0;

                while (true)
                {
                    var bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancelToken)
                                                       .ConfigureAwait(false);
                    if (bytesRead == 0)
                        break;

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
            catch (OperationCanceledException)
            {
                Debug.WriteLine($"[Download] Cancelled during download: {label}");
                shouldDeletePartialFile = true;
                return (false, null, true);
            }
            catch (ObjectDisposedException ex) when (cancelToken.IsCancellationRequested)
            {
                Debug.WriteLine($"[Download] Safe ObjectDisposedException after cancel: {ex.Message}");
                shouldDeletePartialFile = true;
                return (false, null, true);
            }
            catch (IOException ex) when (cancelToken.IsCancellationRequested)
            {
                Debug.WriteLine($"[Download] Safe IOException after cancel: {ex.Message}");
                shouldDeletePartialFile = true;
                return (false, null, true);
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
                    try
                    {
                        CleanupPartialDownload(targetPath);
                    }
                    catch (Exception cleanupEx)
                    {
                        Debug.WriteLine($"[Cleanup] Failed to delete {targetPath}: {cleanupEx.Message}");
                    }
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

