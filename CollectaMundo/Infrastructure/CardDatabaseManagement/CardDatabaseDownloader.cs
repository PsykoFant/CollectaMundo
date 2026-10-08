using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.ApplicationServices.Shared.Operation;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using HttpClient = System.Net.Http.HttpClient;

namespace CollectaMundo.Infrastructure.CardDatabaseManagement
{
    public class CardDatabaseDownloader(HttpClient? httpClient = null) : ICardDatabaseDownloader
    {
        private readonly HttpClient _httpClient = httpClient ?? new HttpClient();

        public async Task<OperationResult> DownloadAsync(string url, string targetPath, string label, int retryDelayInMs, IProgress<string> stepNameAndNumberProgress, IProgress<string> stepDetailAndErrorProgress, IProgress<int>? percentProgress = null, IProgress<bool>? indeterminateProgress = null, CancellationToken cancelToken = default)
        {
            return await RetryHelper.RetryLoopAsync(async () =>
            {
                var (success, error, cancelled) = await DownloadFileAsync(
                    url, targetPath, label,
                    stepDetailAndErrorProgress,
                    percentProgress,
                    indeterminateProgress,
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
            int retryDelayInMs, string stepName, IProgress<string> stepNameAndNumberProgress, IProgress<string> stepDetailAndErrorProgress, IProgress<int>? percentProgress = null, IProgress<bool>? indeterminateProgress = null, CancellationToken cancelToken = default)
        {
            return await RetryHelper.RetryLoopAsync(
                async () =>
                {
                    using var innerCts = new CancellationTokenSource();
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(innerCts.Token, cancelToken);
                    var linkedToken = linkedCts.Token;

                    var task1 = DownloadFileAsync(url1, targetPath1, label1, stepDetailAndErrorProgress, percentProgress, indeterminateProgress, _httpClient, linkedToken);
                    var task2 = DownloadFileAsync(url2, targetPath2, label2, null, null, null, _httpClient, linkedToken);

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

        private static async Task<(bool success, string? errorMessage, bool cancelled)> DownloadFileAsync(string url, string targetPath, string label, IProgress<string>? stepDetailAndErrorProgress, IProgress<int>? percentProgress, IProgress<bool>? indeterminateProgress, HttpClient httpClient, CancellationToken cancelToken)
        {
            Debug.WriteLine($"[Download] Starting download: {label} from {url} to {targetPath}");

            bool shouldDeletePartialFile = false;

            try
            {
                Debug.WriteLine($"[Download] Sending GET request for: {url}");
                var expectedLength = await TryGetContentLengthAsync(url, httpClient, cancelToken);

                using var request =
    new HttpRequestMessage(
        HttpMethod.Get,
        url);

                request.Headers.AcceptEncoding.Clear();
                request.Headers.AcceptEncoding.Add(
                    new System.Net.Http.Headers.StringWithQualityHeaderValue(
                        "identity"));

                using var response =
                    await httpClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancelToken)
                    .ConfigureAwait(false);

                Debug.WriteLine(
    $"[Download headers] " +
    $"HTTP/{response.Version} " +
    $"{(int)response.StatusCode} {response.StatusCode}");

                Debug.WriteLine(
                    $"[Download headers] Final URI: " +
                    $"{response.RequestMessage?.RequestUri}");

                foreach (var header in response.Headers)
                {
                    Debug.WriteLine(
                        $"[Download header] " +
                        $"{header.Key}: {string.Join(", ", header.Value)}");
                }

                foreach (var header in response.Content.Headers)
                {
                    Debug.WriteLine(
                        $"[Download content header] " +
                        $"{header.Key}: {string.Join(", ", header.Value)}");
                }

                if (!response.IsSuccessStatusCode)
                {
                    var msg = $"HTTP error: {(int)response.StatusCode} {response.ReasonPhrase}";
                    Debug.WriteLine($"[Download] {msg}");
                    return (false, msg, false);
                }

                var totalBytes = response.Content.Headers.ContentLength ?? expectedLength;

                var hasKnownLength = totalBytes is > 0;

                percentProgress?.Report(0);
                indeterminateProgress?.Report(!hasKnownLength);
                var buffer = new byte[8192];

                using var contentStream = await response.Content.ReadAsStreamAsync(cancelToken).ConfigureAwait(false);
                using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                if (hasKnownLength)
                {
                    stepDetailAndErrorProgress?.Report($"{label} size: {totalBytes!.Value / 1_000_000.0:0.0} MB");
                }
                else
                {
                    stepDetailAndErrorProgress?.Report($"{label}: downloading...");
                }

                long totalBytesRead = 0;
                int lastReportedPercent = 0;

                while (true)
                {
                    var bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancelToken).ConfigureAwait(false);
                    if (bytesRead == 0)
                        break;

                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancelToken).ConfigureAwait(false);
                    totalBytesRead += bytesRead;

                    if (totalBytes is > 0 && percentProgress != null)
                    {
                        var percent = (int)((double)totalBytesRead / totalBytes.Value * 100);

                        percent = Math.Clamp(percent, 0, 100);

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

                if (hasKnownLength)
                {
                    percentProgress?.Report(100);
                }
                else
                {
                    stepDetailAndErrorProgress?.Report($"{label}: {totalBytesRead / 1_000_000.0:0.0} MB downloaded");
                }

                Debug.WriteLine($"[Download] Completed successfully: {label}");

                Debug.WriteLine(
    $"[Download] {label}: " +
    $"expected={totalBytes?.ToString() ?? "unknown"}, " +
    $"actual={totalBytesRead}");

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
                indeterminateProgress?.Report(false);

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
        private static async Task<long?> TryGetContentLengthAsync(
    string url,
    HttpClient httpClient,
    CancellationToken cancellationToken)
        {
            using var request =
                new HttpRequestMessage(
                    HttpMethod.Get,
                    url);

            request.Headers.Range =
                new System.Net.Http.Headers.RangeHeaderValue(0, 0);

            request.Headers.AcceptEncoding.Clear();
            request.Headers.AcceptEncoding.Add(
                new System.Net.Http.Headers.StringWithQualityHeaderValue(
                    "identity"));

            using var response =
                await httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken)
                .ConfigureAwait(false);

            Debug.WriteLine(
                $"[Range probe] " +
                $"{(int)response.StatusCode} {response.StatusCode}");

            Debug.WriteLine(
                $"[Range probe] Content-Range: " +
                $"{response.Content.Headers.ContentRange}");

            Debug.WriteLine(
                $"[Range probe] Content-Encoding: " +
                $"{string.Join(", ", response.Content.Headers.ContentEncoding)}");

            return response.Content.Headers
                .ContentRange?
                .Length;
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

