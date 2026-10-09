using CollectaMundo.ApplicationServices.CardDatabaseManagement;
using CollectaMundo.ApplicationServices.CardPrices;
using CollectaMundo.ApplicationServices.GenerateMissingPng;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.ApplicationServices.Shared.Progress;
using CollectaMundo.ApplicationServices.Shared.UnitOfWork;
using CollectaMundo.DomainLogic.CardData.Models;
using CollectaMundo.Infrastructure.CardDatabaseManagement;
using CollectaMundo.Infrastructure.CardDatabaseManagement.CardData;
using CollectaMundo.Infrastructure.RemoteLookups;
using CollectaMundo.Infrastructure.Shared.FileTransfers;
using CollectaMundo.Infrastructure.Shared.IO;
using Moq;
using System.Data.SQLite;
using System.IO;

namespace CollectaMundo.Tests.TestUtils
{
    public sealed class FirstTimeSetupTestContext : IDisposable
    {
        public Mock<ICardDatabaseManagementRepo> SchemaRepo { get; }
        public Mock<ICardPriceService> PriceService { get; }
        public Mock<IGenerateMissingPngService> PngService { get; }
        public Mock<IRemoteLookups> RemoteLookups { get; }
        public Mock<IAppSettings> Settings { get; }


        public List<int> PercentSamples { get; }
        public List<bool> VisibleToggles { get; }
        public List<bool> IndeterminateToggles { get; }
        public List<string> Steps { get; }

        private IDisposable? _dbFactoryDisposable;
        private string? _tmpRoot;

        public Mock<ICardDataRepo> CardDataRepo { get; }
        public Mock<IRemoteFileDownloader> RemoteFileDownloaderMock { get; }
        public Mock<IGzipFileDecompressor> GzipFileDecompressorMock { get; }
        private readonly IRemoteFileDownloader? _realRemoteFileDownloader;
        private readonly IGzipFileDecompressor? _realGzipFileDecompressor;
        public IRemoteFileDownloader RemoteFileDownloader => _realRemoteFileDownloader ?? RemoteFileDownloaderMock.Object;
        public IGzipFileDecompressor GzipFileDecompressor => _realGzipFileDecompressor ?? GzipFileDecompressorMock.Object;
        public FirstTimeSetupTestContext(IRemoteFileDownloader? remoteFileDownloaderOverride = null, IGzipFileDecompressor? gzipFileDecompressorOverride = null)
        {
            RemoteFileDownloaderMock = new Mock<IRemoteFileDownloader>();

            GzipFileDecompressorMock = new Mock<IGzipFileDecompressor>();

            if (remoteFileDownloaderOverride is IMocked<IRemoteFileDownloader> mockedDownloader)
            {
                RemoteFileDownloaderMock = mockedDownloader.Mock;
            }
            else if (remoteFileDownloaderOverride is not null)
            {
                _realRemoteFileDownloader = remoteFileDownloaderOverride;
            }

            if (gzipFileDecompressorOverride is IMocked<IGzipFileDecompressor> mockedDecompressor)
            {
                GzipFileDecompressorMock = mockedDecompressor.Mock;
            }
            else if (gzipFileDecompressorOverride is not null)
            {
                _realGzipFileDecompressor = gzipFileDecompressorOverride;
            }

            SchemaRepo = new();
            CardDataRepo = new();
            PriceService = new();
            PngService = new();
            RemoteLookups = new();
            Settings = new();

            PercentSamples = [];
            VisibleToggles = [];
            IndeterminateToggles = [];
            Steps = [];
        }
        public CardDatabaseManagementService BuildService()
        {
            // 1. Create a unique in-memory DB and keep a reference to dispose later
            var dbName = $"cmtests-{Guid.NewGuid():N}";
            var factory = SharedMemoryDbFactory.CreateInMemoryDbFactory(dbName);
            _dbFactoryDisposable = factory as IDisposable;
            var uowRunner = new UnitOfWorkRunner(factory);

            // 2. Set up temp dirs and stubbed settings
            _tmpRoot = Path.Combine(Path.GetTempPath(), "cm-tests", dbName);
            Directory.CreateDirectory(_tmpRoot);

            Settings.Setup(s => s.DatabaseSettings).Returns(new CollectaMundo.ApplicationServices.Shared.DatabaseSettings
            {
                SQLitePath = _tmpRoot
            });
            Settings.Setup(s => s.UserDownloadsPath).Returns(_tmpRoot);
            Settings.Setup(s => s.CardDatabaseUrl).Returns("http://localhost/dummy.sqlite.gz");
            Settings.Setup(s => s.CardPricesUrl).Returns("http://localhost/dummy.json.gz");
            Settings.Setup(s => s.PriceInfo).Returns(new PriceInfo
            {
                Retailer = "CardMarket"
            });

            // 3. Create progress sinks (plain Progress<T> objects, no WPF needed)
            var sinks = new ProgressSinks
            {
                Headline = new InlineProgress<string>(_ => { }),
                Detail = new InlineProgress<string>(_ => { }),
                Step = new InlineProgress<string>(s => Steps.Add(s)),
                Percent = new InlineProgress<int>(p => PercentSamples.Add(p)),
                ProgressBarVisible = new InlineProgress<bool>(v => VisibleToggles.Add(v)),
                ProgressBarIndeterminate = new InlineProgress<bool>(value => IndeterminateToggles.Add(value))
            };

            // 4. Inject everything explicitly (no AppGlobals)
            return new CardDatabaseManagementService(
                Settings.Object,
                factory, // <- directly pass the in-memory connection factory here
                uowRunner,
                sinks,
                SchemaRepo.Object,
                CardDataRepo.Object,
                PriceService.Object,
                PngService.Object,
                RemoteLookups.Object,
                RemoteFileDownloader,
                GzipFileDecompressor
            );
        }
        public void StubAllStepsAsSuccess()
        {
            SchemaRepo
                .Setup(r => r.CreateTablesAsync(
                    It.IsAny<SQLiteConnection>(),
                    It.IsAny<SQLiteTransaction>()))
                .Returns(Task.CompletedTask);

            SchemaRepo
                .Setup(r => r.CreateViewsAsync(
                    It.IsAny<SQLiteConnection>(),
                    It.IsAny<SQLiteTransaction>()))
                .Returns(Task.CompletedTask);

            SchemaRepo
                .Setup(r => r.CreateIndicesAsync(
                    It.IsAny<SQLiteConnection>(),
                    It.IsAny<SQLiteTransaction>()))
                .Returns(Task.CompletedTask);

            SchemaRepo
                .Setup(r => r.OptimizeAsync(It.IsAny<SQLiteConnection>()))
                .Returns(Task.CompletedTask);

            PriceService
                .Setup(p => p.ImportPricesFromJsonAsync(
                    It.IsAny<string>(),
                    It.IsAny<SQLiteConnection>(),
                    It.IsAny<SQLiteTransaction>(),
                    It.IsAny<IProgress<string>?>(),
                    It.IsAny<IProgress<int>?>()))
                .ReturnsAsync(new PriceImportResult("2026-05-30"));

            PngService
                .Setup(p => p.GenerateMissingManaSymbolImagesAsync(
                    It.IsAny<IProgress<int>>()))
                .Returns(Task.CompletedTask);

            PngService
                .Setup(p => p.GenerateMissingManaCostImagesAsync(
                    It.IsAny<IProgress<int>>()))
                .Returns(Task.CompletedTask);

            PngService
                .Setup(p => p.GenerateMissingKeyRuneImagesAsync(
                    It.IsAny<IProgress<int>>()))
                .Returns(Task.CompletedTask);

            RemoteFileDownloaderMock.Setup(d => d.DownloadAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IProgress<FileTransferProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1L);

            GzipFileDecompressorMock.Setup(d => d.DecompressAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IProgress<FileTransferProgress>?>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var candidate = new OracleFaceCandidate(SourceUuid: "test-uuid", ScryfallOracleId: "test-oracle-id", Side: null, Payload: new OracleFacePayload(
                Name: "Test Card",
                ManaCostRaw: null,
                ManaValue: null,
                Colors: null,
                Keywords: null,
                RulesText: null,
                SuperTypes: null,
                Types: "Creature",
                SubTypes: null,
                Type: "Creature"));

            CardDataRepo
                .Setup(r => r.GetOracleFaceCandidatesAsync(
                    It.IsAny<SQLiteConnection>(),
                    It.IsAny<SQLiteTransaction>()))
                .ReturnsAsync([candidate]);

            CardDataRepo
                .Setup(r => r.RebuildCanonicalOracleFacesAsync(
                    It.IsAny<SQLiteConnection>(),
                    It.IsAny<SQLiteTransaction>(),
                    It.IsAny<IReadOnlyList<CanonicalOracleFace>>()))
                .ReturnsAsync(1);
        }
        public void Dispose()
        {
            try
            {
                _dbFactoryDisposable?.Dispose();
                if (!string.IsNullOrEmpty(_tmpRoot) && Directory.Exists(_tmpRoot))
                {
                    Directory.Delete(_tmpRoot, recursive: true);
                }
            }
            catch { /* best effort */ }
        }

        // test helper
        sealed class InlineProgress<T>(Action<T> onReport) : IProgress<T>
        {
            private readonly Action<T> _onReport = onReport;

            public void Report(T value) => _onReport(value);
        }
    }
}
