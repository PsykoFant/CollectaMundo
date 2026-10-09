using CollectaMundo.Infrastructure.Shared.IO;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace CollectaMundo.Infrastructure.Shared.FileTransfers
{
    public sealed class RemoteFileDownloader(HttpClient httpClient) : IRemoteFileDownloader
    {
        private const int BufferSize = 128 * 1024;
        private readonly HttpClient _httpClient = httpClient;
        public async Task<long> DownloadAsync(string url, string destinationPath, IProgress<FileTransferProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(url);
            ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

            var directory = Path.GetDirectoryName(destinationPath);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var partialPath = destinationPath + ".part";

            DeleteIfExists(partialPath);

            try
            {
                long? totalBytes;
                long totalBytesRead = 0;

                using (var request = CreateGetRequest(url))
                using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();

                    totalBytes = response.Content.Headers.ContentLength;
                    totalBytes ??= await TryGetContentLengthFromRangeAsync(url, cancellationToken).ConfigureAwait(false);

                    Debug.WriteLine($"[RemoteFileDownloader] {url}: expected length = {totalBytes?.ToString() ?? "unknown"}");

                    progress?.Report(new FileTransferProgress(BytesTransferred: 0, TotalBytes: totalBytes));

                    await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                    await using var output = new FileStream(partialPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);

                    var buffer = new byte[BufferSize];
                    var lastReportedPercent = -1;

                    while (true)
                    {
                        var bytesRead = await input.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);

                        if (bytesRead == 0)
                        {
                            break;
                        }

                        await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);

                        totalBytesRead += bytesRead;

                        if (progress is null)
                        {
                            continue;
                        }

                        var transferProgress = new FileTransferProgress(totalBytesRead, totalBytes);

                        if (transferProgress.Percent is int percent)
                        {
                            if (percent != lastReportedPercent)
                            {
                                lastReportedPercent = percent;

                                progress.Report(transferProgress);
                            }
                        }
                        else
                        {
                            progress.Report(transferProgress);
                        }
                    }

                    await output.FlushAsync(cancellationToken).ConfigureAwait(false);

                    // input + output are now disposed.
                }

                if (totalBytes is > 0 && totalBytesRead != totalBytes.Value)
                {
                    throw new InvalidDataException($"Remote file length mismatch for '{url}'. Expected {totalBytes.Value:N0} bytes, received {totalBytesRead:N0} bytes.");
                }

                progress?.Report(new FileTransferProgress(BytesTransferred: totalBytesRead, TotalBytes: totalBytes));

                // Safe now: partialPath is no longer open.
                File.Move(partialPath, destinationPath, overwrite: true);

                Debug.WriteLine($"[RemoteFileDownloader] Completed: {url}, {totalBytesRead:N0} bytes.");

                return totalBytesRead;
            }
            catch
            {
                DeleteIfExists(partialPath);
                throw;
            }
        }
        private static HttpRequestMessage CreateGetRequest(string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            request.Headers.AcceptEncoding.Clear();

            request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("identity"));

            return request;
        }
        private async Task<long?> TryGetContentLengthFromRangeAsync(string url, CancellationToken cancellationToken)
        {
            using var request = CreateGetRequest(url);

            request.Headers.Range = new RangeHeaderValue(0, 0);

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.PartialContent)
            {
                return null;
            }

            return response.Content.Headers.ContentRange?.Length;
        }
        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
