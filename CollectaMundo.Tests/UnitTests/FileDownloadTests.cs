using CollectaMundo.Infrastructure.Shared.IO;
using CollectaMundo.Infrastructure.Shared.RemoteFiles;
using CollectaMundo.Tests.TestUtils;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace CollectaMundo.Tests.UnitTests
{
    public class FileDownloadTests
    {
        [Fact]
        public async Task DownloadAsync_DownloadsFileAtomically_AndReportsProgress()
        {
            // Arrange
            var payload = Encoding.UTF8.GetBytes("CollectaMundo download test");

            var requestCount = 0;
            var requestedIdentityEncoding = false;

            var handler = new FakeHttpMessageHandler((request, cancellationToken) =>
                {
                    requestCount++;

                    requestedIdentityEncoding = request.Headers.AcceptEncoding.Any(value => value.Value.Equals("identity", StringComparison.OrdinalIgnoreCase));

                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) });
                });

            using var httpClient = new HttpClient(handler);

            var downloader = new RemoteFileDownloader(httpClient);
            var progressSamples = new List<FileTransferProgress>();
            var progress = new InlineProgress<FileTransferProgress>(value => progressSamples.Add(value));
            var tempDirectory = CreateTempDirectory();
            var targetPath = Path.Combine(tempDirectory, "artifact.gz");

            // Prove that a completed download replaces an old artifact.
            await File.WriteAllTextAsync(targetPath, "old content");

            try
            {
                // Act
                var bytesWritten = await downloader.DownloadAsync("https://fakeurl.com/artifact.gz", targetPath, progress, CancellationToken.None);

                // Assert
                Assert.Equal(payload.LongLength, bytesWritten);
                Assert.Equal(payload, await File.ReadAllBytesAsync(targetPath));
                Assert.False(File.Exists(targetPath + ".part"));
                Assert.Equal(1, requestCount);
                Assert.True(requestedIdentityEncoding);
                Assert.NotEmpty(progressSamples);
                Assert.Equal(0, progressSamples[0].BytesTransferred);
                Assert.Equal((long?)payload.LongLength, progressSamples[0].TotalBytes);
                Assert.Equal(100, progressSamples[^1].Percent);
            }
            finally
            {
                DeleteDirectory(tempDirectory);
            }
        }

        [Fact]
        public async Task DownloadAsync_ThrowsHttpRequestException_On404()
        {
            // Arrange
            var handler = new FakeHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));

            using var httpClient = new HttpClient(handler);

            var downloader = new RemoteFileDownloader(httpClient);
            var tempDirectory = CreateTempDirectory();
            var targetPath = Path.Combine(tempDirectory, "artifact.gz");

            try
            {
                // Act
                var exception = await Assert.ThrowsAsync<HttpRequestException>(() => downloader.DownloadAsync("https://fakeurl.com/404", targetPath, cancellationToken: CancellationToken.None));

                // Assert
                Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
                Assert.False(File.Exists(targetPath));
                Assert.False(File.Exists(targetPath + ".part"));
            }
            finally
            {
                DeleteDirectory(tempDirectory);
            }
        }

        [Fact]
        public async Task DownloadAsync_PropagatesHttpRequestException_WhenNetworkFails()
        {
            // Arrange
            var requestCount = 0;
            var handler = new FakeHttpMessageHandler((_, _) => { requestCount++; return Task.FromException<HttpResponseMessage>(new HttpRequestException("Simulated network failure")); });

            using var httpClient = new HttpClient(handler);

            var downloader = new RemoteFileDownloader(httpClient);
            var tempDirectory = CreateTempDirectory();
            var targetPath = Path.Combine(tempDirectory, "artifact.gz");

            try
            {
                // Act
                var exception = await Assert.ThrowsAsync<HttpRequestException>(() => downloader.DownloadAsync("https://fakeurl.com/artifact.gz", targetPath, cancellationToken: CancellationToken.None));

                // Assert
                Assert.Contains("Simulated network failure", exception.Message);

                // Important architectural assertion:
                // infrastructure no longer owns retry policy.
                Assert.Equal(1, requestCount);
                Assert.False(File.Exists(targetPath + ".part"));
            }
            finally
            {
                DeleteDirectory(tempDirectory);
            }
        }

        [Fact]
        public async Task DownloadAsync_ThrowsCancellation_WhenCancelledBeforeStart()
        {
            // Arrange
            var handler = new FakeHttpMessageHandler(
                    (_, cancellationToken) =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) });
                    });

            using var httpClient = new HttpClient(handler);

            var downloader = new RemoteFileDownloader(httpClient);

            using var cts = new CancellationTokenSource();

            cts.Cancel();

            var tempDirectory = CreateTempDirectory();
            var targetPath = Path.Combine(tempDirectory, "artifact.gz");

            try
            {
                // Act + Assert
                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => downloader.DownloadAsync("https://fakeurl.com/artifact.gz", targetPath, cancellationToken: cts.Token));

                Assert.False(File.Exists(targetPath));
                Assert.False(File.Exists(targetPath + ".part"));
            }
            finally
            {
                DeleteDirectory(tempDirectory);
            }
        }

        [Fact]
        public async Task DownloadAsync_CleansPartialFile_WhenCancelledDuringTransfer()
        {
            // Arrange
            var stream = new BlockingAfterFirstReadStream(Encoding.UTF8.GetBytes("first chunk"));
            var content = new StreamContent(stream);

            // Explicit known size prevents the range fallback from
            // becoming part of this cancellation test.
            content.Headers.ContentLength = 100;

            var handler = new FakeHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content }));

            using var httpClient = new HttpClient(handler);

            var downloader = new RemoteFileDownloader(httpClient);

            using var cts = new CancellationTokenSource();

            cts.CancelAfter(TimeSpan.FromMilliseconds(50));

            var tempDirectory = CreateTempDirectory();
            var targetPath = Path.Combine(tempDirectory, "artifact.gz");

            try
            {
                // Act + Assert
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => downloader.DownloadAsync("https://fakeurl.com/artifact.gz", targetPath, cancellationToken: cts.Token));

                Assert.False(File.Exists(targetPath));
                Assert.False(File.Exists(targetPath + ".part"));
            }
            finally
            {
                DeleteDirectory(tempDirectory);
            }
        }

        [Fact]
        public async Task DownloadAsync_ThrowsAndPreservesExistingDestination_WhenLengthDoesNotMatch()
        {
            // Arrange
            var payload = Encoding.UTF8.GetBytes("incomplete");
            var content = new ByteArrayContent(payload);

            content.Headers.ContentLength = payload.Length + 100;

            var handler = new FakeHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content }));

            using var httpClient = new HttpClient(handler);

            var downloader = new RemoteFileDownloader(httpClient);
            var tempDirectory = CreateTempDirectory();
            var targetPath = Path.Combine(tempDirectory, "artifact.gz");
            await File.WriteAllTextAsync(targetPath, "known-good-old-file");

            try
            {
                // Act
                var exception = await Assert.ThrowsAsync<InvalidDataException>(() => downloader.DownloadAsync("https://fakeurl.com/artifact.gz", targetPath, cancellationToken: CancellationToken.None));

                // Assert
                Assert.Contains("length mismatch", exception.Message, StringComparison.OrdinalIgnoreCase);

                // Atomic publication:
                // failed transfer must not destroy the old good file.
                Assert.Equal("known-good-old-file", await File.ReadAllTextAsync(targetPath));
                Assert.False(File.Exists(targetPath + ".part"));
            }
            finally
            {
                DeleteDirectory(tempDirectory);
            }
        }

        [Fact]
        public async Task DownloadAsync_UsesRangeProbe_WhenContentLengthIsMissing()
        {
            // Arrange
            var payload = Encoding.UTF8.GetBytes("unknown-length-artifact");
            var normalGetCount = 0;
            var rangeProbeCount = 0;

            var handler = new FakeHttpMessageHandler((request, cancellationToken) =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (request.Headers.Range is not null)
                        {
                            rangeProbeCount++;

                            var requestedRange = Assert.Single(request.Headers.Range.Ranges);

                            Assert.Equal(0, requestedRange.From);
                            Assert.Equal(0, requestedRange.To);

                            var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
                            {
                                Content = new ByteArrayContent([payload[0]])
                            };

                            response.Content.Headers.ContentRange = new ContentRangeHeaderValue(0, 0, payload.LongLength);

                            return Task.FromResult(response);
                        }

                        normalGetCount++;

                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new UnknownLengthContent(payload)
                        });
                    });

            using var httpClient = new HttpClient(handler);

            var downloader = new RemoteFileDownloader(httpClient);
            var progressSamples = new List<FileTransferProgress>();
            var tempDirectory = CreateTempDirectory();
            var targetPath = Path.Combine(tempDirectory, "artifact.gz");

            try
            {
                // Act
                var bytesWritten = await downloader.DownloadAsync("https://fakeurl.com/artifact.gz", targetPath, new InlineProgress<FileTransferProgress>(p => progressSamples.Add(p)), CancellationToken.None);

                // Assert
                Assert.Equal(payload.LongLength, bytesWritten);
                Assert.Equal(1, normalGetCount);
                Assert.Equal(1, rangeProbeCount);
                Assert.Equal(payload, await File.ReadAllBytesAsync(targetPath));
                Assert.NotEmpty(progressSamples);
                Assert.All(progressSamples, sample => Assert.Equal((long?)payload.LongLength, sample.TotalBytes));
                Assert.Equal(100, progressSamples[^1].Percent);
            }
            finally
            {
                DeleteDirectory(tempDirectory);
            }
        }
        private static string CreateTempDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), "CollectaMundo.Tests", Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(path);

            return path;
        }
        private static void DeleteDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        private sealed class InlineProgress<T>(Action<T> onReport) : IProgress<T>
        {
            public void Report(T value)
            {
                onReport(value);
            }
        }
        private sealed class UnknownLengthContent(byte[] payload) : HttpContent
        {
            protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            {
                await stream.WriteAsync(payload);
            }

            protected override bool TryComputeLength(out long length)
            {
                length = 0;
                return false;
            }
        }
        private sealed class BlockingAfterFirstReadStream(byte[] firstChunk) : Stream
        {
            private bool _firstReadCompleted;
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }
            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                if (!_firstReadCompleted)
                {
                    _firstReadCompleted = true;

                    var bytesToCopy = Math.Min(buffer.Length, firstChunk.Length);

                    firstChunk.AsSpan(0, bytesToCopy).CopyTo(buffer.Span);

                    return ValueTask.FromResult(bytesToCopy);
                }

                return WaitForCancellationAsync(
                    cancellationToken);
            }
            private static async ValueTask<int> WaitForCancellationAsync(CancellationToken cancellationToken)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

                return 0;
            }
            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }
            public override void Flush()
            {
            }
            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }
            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }
            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }
        }
    }
}
