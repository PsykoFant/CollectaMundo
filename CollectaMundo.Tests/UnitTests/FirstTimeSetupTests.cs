using CollectaMundo.ApplicationServices.Shared.Operation;
using CollectaMundo.Infrastructure.Shared.IO;
using CollectaMundo.Tests.TestUtils;
using Moq;
using System.Data.SQLite;
using System.IO;
using System.Net.Http;


namespace CollectaMundo.Tests.UnitTests
{
    public class FirstTimeSetupTests
    {
        private const string DatabaseUrl = "http://localhost/dummy.sqlite.gz";
        private const string PricesUrl = "http://localhost/dummy.json.gz";

        [Fact]
        public async Task FirstTimeDbPrepOrchetrator_AllStepsSucceed_ReturnsSuccess_AndProgressFinishes()
        {
            using var ctx = new FirstTimeSetupTestContext();
            ctx.RemoteLookups.Setup(r => r.IsInternetAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
            ctx.StubAllStepsAsSuccess();

            var svc = ctx.BuildService();

            var result = await svc.FirstTimeDbPrepOrchestrator(0);

            Assert.Equal(OperationResultCode.Success, result.Code);

            // repo/service calls
            ctx.SchemaRepo.Verify(r => r.CreateTablesAsync(It.IsAny<SQLiteConnection>(), It.IsAny<SQLiteTransaction>()), Times.Once);

            ctx.RemoteFileDownloaderMock.Verify(d => d.DownloadAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IProgress<FileTransferProgress>?>(),
                It.IsAny<CancellationToken>()),
                Times.Exactly(2));

            ctx.GzipFileDecompressorMock.Verify(d => d.DecompressAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IProgress<FileTransferProgress>?>(),
                It.IsAny<CancellationToken>()),
                Times.Exactly(2));

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

            int createCalls = 0;
            ctx.SchemaRepo.Setup(r => r.CreateTablesAsync(It.IsAny<SQLiteConnection>(), It.IsAny<SQLiteTransaction>())).Returns(async () => { createCalls++; await Task.Yield(); throw new Exception("Step 2 fails"); });

            var svc = ctx.BuildService();

            var result = await svc.FirstTimeDbPrepOrchestrator(0);

            Assert.Equal(OperationResultCode.Error, result.Code);
            Assert.Equal(3, createCalls); // max retries
            ctx.SchemaRepo.Verify(r => r.CreateViewsAsync(It.IsAny<SQLiteConnection>(), It.IsAny<SQLiteTransaction>()), Times.Never);
        }

        [Fact]
        public async Task UpdateDbPrepOrchetrator_OneArtifactFails_ReturnsDownloadFailed_AndStopsPipeline()
        {
            using var ctx = new FirstTimeSetupTestContext();

            ctx.RemoteLookups
                .Setup(r => r.IsInternetAvailableAsync(
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            ctx.StubAllStepsAsSuccess();

            ctx.RemoteFileDownloaderMock
                .Setup(d => d.DownloadAsync(
                    PricesUrl,
                    It.IsAny<string>(),
                    It.IsAny<IProgress<FileTransferProgress>?>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(
                    new HttpRequestException(
                        "One file failed"));

            var svc = ctx.BuildService();

            var result =
                await svc.UpdateDbPrepOrchestrator(
                    0,
                    CancellationToken.None);

            Assert.Equal(
                OperationResultCode.DownloadFailed,
                result.Code);

            ctx.RemoteFileDownloaderMock.Verify(
                d => d.DownloadAsync(
                    PricesUrl,
                    It.IsAny<string>(),
                    It.IsAny<IProgress<FileTransferProgress>?>(),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(3));

            ctx.SchemaRepo.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task NoInternet_ReturnsNoInternet_AndSkipsEverything()
        {
            using var ctx = new FirstTimeSetupTestContext();
            ctx.RemoteLookups.Setup(r => r.IsInternetAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

            var svc = ctx.BuildService();
            var result = await svc.FirstTimeDbPrepOrchestrator(0);

            Assert.Equal(OperationResultCode.NoInternet, result.Code);

            ctx.RemoteFileDownloaderMock.VerifyNoOtherCalls();
            ctx.GzipFileDecompressorMock.VerifyNoOtherCalls();
            ctx.SchemaRepo.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task UpdateDbPrepOrchetrator_UserCancelsBeforeAcquisition_ReturnsCancelledByUser()
        {
            using var ctx = new FirstTimeSetupTestContext();

            ctx.RemoteLookups
                .Setup(r => r.IsInternetAvailableAsync(
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            ctx.StubAllStepsAsSuccess();

            using var cts =
                new CancellationTokenSource();

            cts.Cancel();

            var svc = ctx.BuildService();

            var result =
                await svc.UpdateDbPrepOrchestrator(
                    0,
                    cts.Token);

            Assert.Equal(
                OperationResultCode.CancelledByUser,
                result.Code);

            ctx.RemoteFileDownloaderMock.VerifyNoOtherCalls();
            ctx.GzipFileDecompressorMock.VerifyNoOtherCalls();
            ctx.SchemaRepo.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task FirstTimeDbPrepOrchetrator_DownloadThrowsHttpException_ReturnsDownloadFailed()
        {
            using var ctx = new FirstTimeSetupTestContext();

            ctx.RemoteLookups
                .Setup(r => r.IsInternetAvailableAsync(
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            ctx.StubAllStepsAsSuccess();

            // Override the default successful downloader setup.
            ctx.RemoteFileDownloaderMock
                .Setup(d => d.DownloadAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IProgress<FileTransferProgress>?>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(
                    new HttpRequestException("boom"));

            var svc = ctx.BuildService();

            var result =
                await svc.FirstTimeDbPrepOrchestrator(0);

            Assert.Equal(
                OperationResultCode.DownloadFailed,
                result.Code);
        }

        [Fact]
        public async Task UpdateDbPrepOrchetrator_ProgressReportsBeforeCancel_AreCaptured()
        {
            using var ctx = new FirstTimeSetupTestContext();

            ctx.RemoteLookups
                .Setup(r => r.IsInternetAvailableAsync(
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            ctx.StubAllStepsAsSuccess();

            using var cts =
                new CancellationTokenSource();

            // Override only the database artifact.
            // Leave the price artifact using the normal successful stub.
            ctx.RemoteFileDownloaderMock
                .Setup(d => d.DownloadAsync(
                    It.Is<string>(
                        url => url.Contains(
                            "dummy.sqlite",
                            StringComparison.OrdinalIgnoreCase)),
                    It.IsAny<string>(),
                    It.IsAny<IProgress<FileTransferProgress>?>(),
                    It.IsAny<CancellationToken>()))
                .Returns((
                    string _,
                    string __,
                    IProgress<FileTransferProgress>? progress,
                    CancellationToken ___) =>
                {
                    progress?.Report(
                        new FileTransferProgress(
                            BytesTransferred: 15,
                            TotalBytes: 100));

                    progress?.Report(
                        new FileTransferProgress(
                            BytesTransferred: 50,
                            TotalBytes: 100));

                    cts.Cancel();

                    return Task.FromCanceled<long>(
                        cts.Token);
                });

            var svc = ctx.BuildService();

            var result =
                await svc.UpdateDbPrepOrchestrator(
                    0,
                    cts.Token);

            Assert.Equal(
                OperationResultCode.CancelledByUser,
                result.Code);

            Assert.Contains(
                ctx.PercentSamples,
                p => p == 50);
        }

        [Fact]
        public async Task UpdateDbPrepOrchetrator_CancelDuringRetryDelay_AbortsImmediately()
        {
            using var ctx = new FirstTimeSetupTestContext();
            ctx.RemoteLookups.Setup(r => r.IsInternetAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
            ctx.StubAllStepsAsSuccess();

            var cts = new CancellationTokenSource();

            var callCount = 0;

            ctx.RemoteFileDownloaderMock
                .Setup(d => d.DownloadAsync(
                    DatabaseUrl,
                    It.IsAny<string>(),
                    It.IsAny<IProgress<FileTransferProgress>?>(),
                    It.IsAny<CancellationToken>()))
                .Returns((
                    string _,
                    string __,
                    IProgress<FileTransferProgress>? ___,
                    CancellationToken ____) =>
                {
                    Interlocked.Increment(
                        ref callCount);

                    cts.Cancel();

                    return Task.FromException<long>(
                        new HttpRequestException(
                            "Simulated failure"));
                });


            var svc = ctx.BuildService();
            var result = await svc.UpdateDbPrepOrchestrator(0, cts.Token);

            Assert.Equal(OperationResultCode.CancelledByUser, result.Code);
            Assert.Equal(1, callCount); // Should abort before retrying
        }

        [Fact]
        public async Task FirstTimeDbPrepOrchetrator_TransientDownloadFailures_AreRetried_AndRecover()
        {
            using var ctx =
                new FirstTimeSetupTestContext();

            ctx.RemoteLookups
                .Setup(r => r.IsInternetAvailableAsync(
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            ctx.StubAllStepsAsSuccess();

            var databaseAttempts = 0;
            var priceAttempts = 0;

            ctx.RemoteFileDownloaderMock
                .Setup(d => d.DownloadAsync(
                    DatabaseUrl,
                    It.IsAny<string>(),
                    It.IsAny<IProgress<FileTransferProgress>?>(),
                    It.IsAny<CancellationToken>()))
                .Returns((
                    string _,
                    string __,
                    IProgress<FileTransferProgress>? ___,
                    CancellationToken ____) =>
                {
                    var attempt =
                        Interlocked.Increment(
                            ref databaseAttempts);

                    return attempt == 1
                        ? Task.FromException<long>(
                            new HttpRequestException(
                                "Transient DB failure"))
                        : Task.FromResult(1L);
                });

            ctx.RemoteFileDownloaderMock
                .Setup(d => d.DownloadAsync(
                    PricesUrl,
                    It.IsAny<string>(),
                    It.IsAny<IProgress<FileTransferProgress>?>(),
                    It.IsAny<CancellationToken>()))
                .Returns((
                    string _,
                    string __,
                    IProgress<FileTransferProgress>? ___,
                    CancellationToken ____) =>
                {
                    var attempt =
                        Interlocked.Increment(
                            ref priceAttempts);

                    return attempt == 1
                        ? Task.FromException<long>(
                            new HttpRequestException(
                                "Transient price failure"))
                        : Task.FromResult(1L);
                });

            var svc =
                ctx.BuildService();

            var result =
                await svc.FirstTimeDbPrepOrchestrator(0);

            Assert.Equal(
                OperationResultCode.Success,
                result.Code);

            Assert.Equal(
                2,
                databaseAttempts);

            Assert.Equal(
                2,
                priceAttempts);

            ctx.PriceService.Verify(
                p => p.ImportPricesFromJsonAsync(
                    It.IsAny<string>(),
                    It.IsAny<SQLiteConnection>(),
                    It.IsAny<SQLiteTransaction>(),
                    It.IsAny<IProgress<string>?>(),
                    It.IsAny<IProgress<int>?>()),
                Times.Once);
        }

        [Fact]
        public async Task FirstTimeDbPrepOrchetrator_DecompressionFails_ReturnsDownloadFailed_AndStopsPipeline()
        {
            using var ctx =
                new FirstTimeSetupTestContext();

            ctx.RemoteLookups
                .Setup(r => r.IsInternetAvailableAsync(
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            ctx.StubAllStepsAsSuccess();

            ctx.GzipFileDecompressorMock
                .Setup(d => d.DecompressAsync(
                    It.IsAny<string>(),
                    It.Is<string>(
                        path => path.EndsWith(
                            "AllPrintings.sqlite",
                            StringComparison.OrdinalIgnoreCase)),
                    It.IsAny<IProgress<FileTransferProgress>?>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(
                    new InvalidDataException(
                        "Corrupt gzip"));

            var svc =
                ctx.BuildService();

            var result =
                await svc.FirstTimeDbPrepOrchestrator(0);

            Assert.Equal(
                OperationResultCode.DownloadFailed,
                result.Code);

            ctx.SchemaRepo.Verify(
                r => r.CreateTablesAsync(
                    It.IsAny<SQLiteConnection>(),
                    It.IsAny<SQLiteTransaction>()),
                Times.Never);
        }

    }
}
