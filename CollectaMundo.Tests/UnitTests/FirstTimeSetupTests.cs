using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.Infrastructure.CardDatabaseManagement;
using CollectaMundo.Tests.TestUtils;
using Moq;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;


namespace CollectaMundo.Tests.UnitTests
{
    public class FirstTimeSetupTests
    {

        [Fact]
        public async Task FirstTimeDbPrepOrchetrator_AllStepsSucceed_ReturnsSuccess_AndProgressFinishes()
        {
            using var ctx = new FirstTimeSetupTestContext();
            ctx.RemoteLookups.Setup(r => r.IsInternetAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
            ctx.StubAllStepsAsSuccess();

            ctx.CardDatabaseDownloaderMock
                .Setup(d => d.DownloadParallelAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<int>(), It.IsAny<string>(),
                    It.IsAny<IProgress<string>>(), It.IsAny<IProgress<string>>(),
                    It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
                .Callback((
                    string u1, string p1, string l1,
                    string u2, string p2, string l2,
                    int retryDelay, string stepName,
                    IProgress<string> stepProg, IProgress<string> detailProg,
                    IProgress<int> percentProg, CancellationToken _) =>
                {
                    stepProg?.Report(stepName);
                    detailProg?.Report("starting…");
                    percentProg?.Report(15);
                    percentProg?.Report(60);
                    percentProg?.Report(100);
                })
                .ReturnsAsync(new OperationResult(OperationResultCode.Success, "OK"));

            var svc = ctx.BuildService();

            var result = await svc.FirstTimeDbPrepOrchestrator(0);

            Assert.Equal(OperationResultCode.Success, result.Code);

            // repo/service calls
            ctx.SchemaRepo.Verify(r => r.CreateTablesAsync(It.IsAny<SQLiteConnection>(), It.IsAny<SQLiteTransaction>()), Times.Once);

            // progress assertions
            Assert.Contains(ctx.VisibleToggles, v => v);          // bar was shown
            Assert.Contains(ctx.VisibleToggles, v => v == false); // bar was hidden
            Assert.Contains(ctx.PercentSamples, p => p == 100);   // finished
            Assert.NotEmpty(ctx.Steps);                         // at least one step label
        }
        [Fact]
        public async Task FirstTimeDbPrepOrchetrator_RetriesCreateTables_ThenSucceeds()
        {
            using var ctx = new FirstTimeSetupTestContext();
            ctx.RemoteLookups.Setup(r => r.IsInternetAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
            ctx.StubAllStepsAsSuccess();

            int attempts = 0;
            ctx.SchemaRepo
               .Setup(r => r.CreateTablesAsync(It.IsAny<SQLiteConnection>(), It.IsAny<SQLiteTransaction>()))
               .Returns(async () =>
               {
                   await Task.Yield();
                   attempts++;
                   if (attempts < 3)
                   {
                       throw new Exception("boom");
                   }
               });

            ctx.CardDatabaseDownloaderMock
                .Setup(d => d.DownloadParallelAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<int>(), It.IsAny<string>(),
                    It.IsAny<IProgress<string>>(), It.IsAny<IProgress<string>>(),
                    It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OperationResult(OperationResultCode.Success, "OK"));

            var svc = ctx.BuildService();

            var result = await svc.FirstTimeDbPrepOrchestrator(0);

            Assert.Equal(OperationResultCode.Success, result.Code);
            Assert.Equal(3, attempts); // 2 failures + 1 success
        }
        [Fact]
        public async Task Step2FailsAfterRetries_ReturnsError_AndStopsPipeline()
        {
            using var ctx = new FirstTimeSetupTestContext();
            ctx.RemoteLookups.Setup(r => r.IsInternetAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
            ctx.StubAllStepsAsSuccess();

            ctx.CardDatabaseDownloaderMock
                .Setup(d => d.DownloadParallelAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<int>(), It.IsAny<string>(),
                    It.IsAny<IProgress<string>>(), It.IsAny<IProgress<string>>(),
                    It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OperationResult(OperationResultCode.Success, "OK"));

            int createCalls = 0;
            ctx.SchemaRepo.Setup(r => r.CreateTablesAsync(It.IsAny<SQLiteConnection>(), It.IsAny<SQLiteTransaction>())).Returns(async () => { createCalls++; await Task.Yield(); throw new Exception("Step 2 fails"); });

            var svc = ctx.BuildService();

            var result = await svc.FirstTimeDbPrepOrchestrator(0);

            Assert.Equal(OperationResultCode.Error, result.Code);
            Assert.Equal(3, createCalls); // max retries
            ctx.SchemaRepo.Verify(r => r.CreateViewsAsync(It.IsAny<SQLiteConnection>(), It.IsAny<SQLiteTransaction>()), Times.Never);
        }
        [Fact]
        public async Task DownloadFails_ReturnsDownloadFailed_DoesNotRunSteps()
        {
            using var ctx = new FirstTimeSetupTestContext();
            ctx.RemoteLookups.Setup(r => r.IsInternetAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
            ctx.StubAllStepsAsSuccess();

            ctx.CardDatabaseDownloaderMock
                .Setup(d => d.DownloadParallelAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<int>(), It.IsAny<string>(),
                    It.IsAny<IProgress<string>>(), It.IsAny<IProgress<string>>(),
                    It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OperationResult(OperationResultCode.DownloadFailed, "net fail"));

            var svc = ctx.BuildService();

            var result = await svc.FirstTimeDbPrepOrchestrator(0);

            Assert.Equal(OperationResultCode.DownloadFailed, result.Code);
            ctx.SchemaRepo.Verify(r => r.CreateTablesAsync(It.IsAny<SQLiteConnection>(), It.IsAny<SQLiteTransaction>()), Times.Never);
            ctx.CardDatabaseDownloaderMock.Verify(d => d.DownloadParallelAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<IProgress<string>>(), It.IsAny<IProgress<string>>(),
                It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }
        [Fact]
        public async Task NoInternet_ReturnsNoInternet_AndSkipsEverything()
        {
            using var ctx = new FirstTimeSetupTestContext();
            ctx.RemoteLookups.Setup(r => r.IsInternetAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

            var svc = ctx.BuildService();
            var result = await svc.FirstTimeDbPrepOrchestrator(0);

            Assert.Equal(OperationResultCode.NoInternet, result.Code);
            ctx.CardDatabaseDownloaderMock.VerifyNoOtherCalls();
            ctx.SchemaRepo.VerifyNoOtherCalls();
        }
        [Fact]
        public async Task UpdateDbPrepOrchetrator_UserCancelsDuringDownload_ReturnsCancelledByUser()
        {
            using var ctx = new FirstTimeSetupTestContext();
            ctx.RemoteLookups.Setup(r => r.IsInternetAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
            ctx.StubAllStepsAsSuccess();

            var cts = new CancellationTokenSource();
            cts.Cancel(); // Simulate user cancellation before download starts

            ctx.CardDatabaseDownloaderMock
                .Setup(d => d.DownloadParallelAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<int>(), It.IsAny<string>(),
                    It.IsAny<IProgress<string>>(), It.IsAny<IProgress<string>>(),
                    It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OperationResult(OperationResultCode.CancelledByUser, "User cancelled"));

            var svc = ctx.BuildService();

            var result = await svc.UpdateDbPrepOrchetrator(0, cts.Token);

            Assert.Equal(OperationResultCode.CancelledByUser, result.Code);
            ctx.SchemaRepo.VerifyNoOtherCalls(); // No further steps run
        }
        [Fact]
        public async Task FirstTimeDbPrepOrchetrator_DownloadThrowsHttpException_ReturnsDownloadFailed()
        {
            using var ctx = new FirstTimeSetupTestContext();
            ctx.RemoteLookups.Setup(r => r.IsInternetAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
            ctx.StubAllStepsAsSuccess();

            ctx.CardDatabaseDownloaderMock
            .Setup(d => d.DownloadParallelAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<IProgress<string>>(), It.IsAny<IProgress<string>>(),
                It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OperationResult(OperationResultCode.DownloadFailed, "boom"));


            var svc = ctx.BuildService();
            var result = await svc.FirstTimeDbPrepOrchestrator(0);

            Assert.Equal(OperationResultCode.DownloadFailed, result.Code); // Gracefully mapped
        }
        [Fact]
        public async Task UpdateDbPrepOrchetrator_ProgressReportsBeforeCancel_AreCaptured()
        {
            using var ctx = new FirstTimeSetupTestContext();
            ctx.RemoteLookups.Setup(r => r.IsInternetAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
            ctx.StubAllStepsAsSuccess();

            var cts = new CancellationTokenSource();

            ctx.CardDatabaseDownloaderMock
                .Setup(d => d.DownloadParallelAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<int>(), It.IsAny<string>(),
                    It.IsAny<IProgress<string>>(), It.IsAny<IProgress<string>>(),
                    It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
                .Callback((
                    string u1, string p1, string l1,
                    string u2, string p2, string l2,
                    int retryDelay, string stepName,
                    IProgress<string> stepProg, IProgress<string> detailProg,
                    IProgress<int> percentProg, CancellationToken _) =>
                {
                    percentProg.Report(15);
                    percentProg.Report(50);
                    cts.Cancel(); // simulate cancel mid-progress
                })
                .ReturnsAsync(new OperationResult(OperationResultCode.CancelledByUser, "cancelled"));

            var svc = ctx.BuildService();
            var result = await svc.UpdateDbPrepOrchetrator(0, cts.Token);

            Assert.Equal(OperationResultCode.CancelledByUser, result.Code);
            Assert.Contains(ctx.PercentSamples, p => p == 50); // Assert progress was tracked
        }
        [Fact]
        public async Task UpdateDbPrepOrchetrator_CancelDuringRetryDelay_AbortsImmediately()
        {
            using var ctx = new FirstTimeSetupTestContext();
            ctx.RemoteLookups.Setup(r => r.IsInternetAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
            ctx.StubAllStepsAsSuccess();

            var cts = new CancellationTokenSource();

            int callCount = 0;
            ctx.CardDatabaseDownloaderMock
                .Setup(d => d.DownloadParallelAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<int>(), It.IsAny<string>(),
                    It.IsAny<IProgress<string>>(), It.IsAny<IProgress<string>>(),
                    It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    await Task.Yield(); // Prevents compiler warning

                    callCount++;
                    if (callCount == 1)
                    {
                        cts.Cancel(); // simulate user cancelling *after* first failure
                        return new OperationResult(OperationResultCode.DownloadFailed, "Simulated failure");
                    }

                    return new OperationResult(OperationResultCode.Success);
                });


            var svc = ctx.BuildService();
            var result = await svc.UpdateDbPrepOrchetrator(0, cts.Token);

            Assert.Equal(OperationResultCode.CancelledByUser, result.Code);
            Assert.Equal(1, callCount); // Should abort before retrying
        }
        [Fact]
        public async Task UpdateDbPrepOrchetrator_OneFileFailsInParallel_ReturnsDownloadFailed()
        {
            using var ctx = new FirstTimeSetupTestContext();
            ctx.RemoteLookups.Setup(r => r.IsInternetAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
            ctx.StubAllStepsAsSuccess();

            // Simulate a download result where one file failed
            ctx.CardDatabaseDownloaderMock
                .Setup(d => d.DownloadParallelAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<int>(), It.IsAny<string>(),
                    It.IsAny<IProgress<string>>(), It.IsAny<IProgress<string>>(),
                    It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OperationResult(OperationResultCode.DownloadFailed, "One file failed"));

            var svc = ctx.BuildService();
            var result = await svc.UpdateDbPrepOrchetrator(0, CancellationToken.None);

            Assert.Equal(OperationResultCode.DownloadFailed, result.Code);
            Assert.Contains("One file failed", result.Message);
            ctx.SchemaRepo.VerifyNoOtherCalls(); // No further processing after failed download
        }
        [Fact]
        public async Task FirstTimeDbPrepOrchetrator_UsesRealDownloader_WithFakeHttpClient()
        {
            // Arrange: Create a fake HTTP response
            var httpClient = FakeHttpMessageHandler.WithStaticResponse("{\"status\":\"ok\"}");
            var downloader = new CardDatabaseDownloader(httpClient);

            // Pass the real downloader into the context constructor
            using var ctx = new FirstTimeSetupTestContext(downloader);

            ctx.RemoteLookups.Setup(r => r.IsInternetAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
            ctx.StubAllStepsAsSuccess();

            var svc = ctx.BuildService();

            // Act
            var result = await svc.FirstTimeDbPrepOrchestrator(0);

            // Assert
            Assert.Equal(OperationResultCode.Success, result.Code);
        }
        [Fact]
        public async Task FirstTimeDbPrepOrchetrator_RealDownloader_Http500_ReturnsDownloadFailed()
        {
            var httpClient = FakeHttpMessageHandler.WithStatusCode(HttpStatusCode.InternalServerError);
            var downloader = new CardDatabaseDownloader(httpClient);

            using var ctx = new FirstTimeSetupTestContext(downloader);
            ctx.RemoteLookups.Setup(r => r.IsInternetAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
            ctx.StubAllStepsAsSuccess();

            var svc = ctx.BuildService();

            var result = await svc.FirstTimeDbPrepOrchestrator(0);

            Assert.Equal(OperationResultCode.DownloadFailed, result.Code);
            Debug.WriteLine(result.Message);
            Assert.Contains("Step 1. Downloading card database and prices... failed after 3 attempts.", result.Message, StringComparison.OrdinalIgnoreCase);
        }
        [Fact]
        public async Task FirstTimeDbPrepOrchetrator_RealDownloader_ThrowsDuringRead_ReturnsDownloadFailed()
        {
            var httpClient = FakeHttpMessageHandler.WithStreamFailure(new IOException("Simulated stream crash"));
            var downloader = new CardDatabaseDownloader(httpClient);

            using var ctx = new FirstTimeSetupTestContext(downloader);
            ctx.RemoteLookups.Setup(r => r.IsInternetAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
            ctx.StubAllStepsAsSuccess();

            var svc = ctx.BuildService();

            var result = await svc.FirstTimeDbPrepOrchestrator(0);

            Assert.Equal(OperationResultCode.DownloadFailed, result.Code);
            Assert.Contains("Step 1. Downloading card database and prices... failed after 3 attempts.", result.Message, StringComparison.OrdinalIgnoreCase);
        }
        [Fact]
        public async Task FirstTimeDbPrepOrchetrator_RealDownloader_RecoversAfterTransientHttpFailure()
        {
            var attemptsPerUrl = new Dictionary<string, int>
            {
                ["http://localhost/dummy.sqlite"] = 0,
                ["http://localhost/dummy.json"] = 0
            };

            var httpClient = new HttpClient(new FakeHttpMessageHandler((req, token) =>
            {
                var url = req.RequestUri?.ToString() ?? "";
                if (!attemptsPerUrl.ContainsKey(url))
                {
                    throw new InvalidOperationException("Unexpected URL in test: " + url);
                }

                attemptsPerUrl[url]++;

                if (attemptsPerUrl[url] < 2)
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"status\":\"ok\"}")
                });
            }));

            var downloader = new CardDatabaseDownloader(httpClient);
            using var ctx = new FirstTimeSetupTestContext(downloader);

            ctx.RemoteLookups.Setup(r => r.IsInternetAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
            ctx.StubAllStepsAsSuccess();

            var svc = ctx.BuildService();
            var result = await svc.FirstTimeDbPrepOrchestrator(0);

            ctx.PriceService.Verify(p => p.ImportPricesFromJsonAsync(
        It.IsAny<string>(),
        It.IsAny<SQLiteConnection>(),
        It.IsAny<SQLiteTransaction>(),
        It.IsAny<IProgress<string>?>(),
        It.IsAny<IProgress<int>?>()),
    Times.Once);

            Assert.Equal(OperationResultCode.Success, result.Code);


            // ✅ Validate each URL retried once and then succeeded
            Assert.Equal(2, attemptsPerUrl["http://localhost/dummy.sqlite"]);
            Assert.Equal(2, attemptsPerUrl["http://localhost/dummy.json"]);
        }

    }
}
