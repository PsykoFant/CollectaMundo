using CollectaMundo.ApplicationServices.Shared.Operation;
using CollectaMundo.Infrastructure.CardDatabaseManagement;
using CollectaMundo.Tests.TestUtils;
using System.IO;
using System.Net;
using System.Net.Http;

namespace CollectaMundo.Tests.UnitTests
{
    public class FileDownloadTests
    {
        [Fact]
        public async Task DownloadAsync_ReturnsError_On404()
        {
            var handler = new FakeHttpMessageHandler((req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));

            var httpClient = new HttpClient(handler);
            var downloader = new CardDatabaseDownloader(httpClient);

            var result = await downloader.DownloadAsync(
                url: "https://fakeurl.com/404",
                targetPath: Path.GetTempFileName(),
                label: "Test 404",
                retryDelayInMs: 10,
                stepNameAndNumberProgress: new NullProgress<string>(),
                stepDetailAndErrorProgress: new NullProgress<string>(),
                cancelToken: CancellationToken.None
            );

            Assert.Equal(OperationResultCode.Error, result.Code);
            Assert.Contains("404", result.Message);
        }
        [Fact]
        public async Task DownloadAsync_ReturnsError_WhenNoInternet()
        {
            // Arrange: Simulate no internet using a handler that fails immediately
            var handler = new SocketsHttpHandler
            {
                ConnectCallback = (_, _) => new ValueTask<Stream>(Task.FromException<Stream>(new HttpRequestException("Simulated no internet connection")))
            };

            using var httpClient = new HttpClient(handler);

            var noopStepProgress = new Progress<string>(_ => { });
            var noopDetailProgress = new Progress<string>(_ => { });

            var downloader = new CardDatabaseDownloader(httpClient);

            // Act
            var result = await downloader.DownloadAsync(
                url: "http://fake-url",
                targetPath: Path.GetTempFileName(),
                label: "Test No Internet",
                retryDelayInMs: 0,
                stepNameAndNumberProgress: noopStepProgress,
                stepDetailAndErrorProgress: noopDetailProgress,
                cancelToken: CancellationToken.None
            );

            // Assert
            Assert.Equal(OperationResultCode.Error, result.Code);
            Assert.Contains("failed after", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task DownloadAsync_CancelsDuringRetry()
        {
            var handler = new FakeHttpMessageHandler((req, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)); // 503
            });

            var httpClient = new HttpClient(handler);
            var downloader = new CardDatabaseDownloader(httpClient);

            using var cts = new CancellationTokenSource();
            var cancelDelayTask = Task.Delay(20).ContinueWith(_ => cts.Cancel());

            var result = await downloader.DownloadAsync(
                url: "https://fakeurl.com/slow",
                targetPath: Path.GetTempFileName(),
                label: "Test Cancel",
                retryDelayInMs: 50,
                stepNameAndNumberProgress: new NullProgress<string>(),
                stepDetailAndErrorProgress: new NullProgress<string>(),
                cancelToken: cts.Token
            );

            Assert.Equal(OperationResultCode.CancelledByUser, result.Code);
        }
        [Fact]
        public async Task DownloadAsync_ReturnsCancelled_WhenCancelledBeforeStart()
        {
            // Arrange
            var handler = new SocketsHttpHandler
            {
                ConnectCallback = (_, _) =>
                    new ValueTask<Stream>(Task.FromException<Stream>(
                        new HttpRequestException("This should not be hit — test should cancel first")))
            };

            var httpClient = new HttpClient(handler);

            var noopStepProgress = new Progress<string>(_ => { });
            var noopDetailProgress = new Progress<string>(_ => { });

            var downloader = new CardDatabaseDownloader(httpClient);

            // Act
            using var cts = new CancellationTokenSource();
            cts.Cancel(); // cancel BEFORE download starts

            var result = await downloader.DownloadAsync(
                url: "http://fake-url",
                targetPath: Path.GetTempFileName(),
                label: "Test Cancel Before Start",
                retryDelayInMs: 0,
                stepNameAndNumberProgress: noopStepProgress,
                stepDetailAndErrorProgress: noopDetailProgress,
                cancelToken: cts.Token
            );

            // Assert
            Assert.Equal(OperationResultCode.CancelledByUser, result.Code);
            Assert.Contains("cancel", result.Message, StringComparison.OrdinalIgnoreCase);
        }

    }
}
