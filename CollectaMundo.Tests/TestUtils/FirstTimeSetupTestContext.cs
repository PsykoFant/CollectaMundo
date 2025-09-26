using CollectaMundo.ApplicationServices.CardDatabaseManagement;
using CollectaMundo.ApplicationServices.CardPrices;
using CollectaMundo.ApplicationServices.GenerateMissingPng;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.ApplicationServices.Shared.Progress;
using CollectaMundo.Infrastructure.CardDatabaseManagement;
using CollectaMundo.Infrastructure.RemoteLookups;
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
        public List<string> Steps { get; }

        private IDisposable? _dbFactoryDisposable;
        private string? _tmpRoot;

        public Mock<ICardDatabaseDownloader> CardDatabaseDownloaderMock { get; }

        private readonly ICardDatabaseDownloader? _realDownloader;
        public ICardDatabaseDownloader CardDatabaseDownloader => _realDownloader ?? CardDatabaseDownloaderMock.Object;
        public FirstTimeSetupTestContext(ICardDatabaseDownloader? downloaderOverride = null)
        {
            // Always create a mock to prevent null refs
            CardDatabaseDownloaderMock = new Mock<ICardDatabaseDownloader>();

            if (downloaderOverride is IMocked<ICardDatabaseDownloader> mocked)
            {
                CardDatabaseDownloaderMock = mocked.Mock;
            }
            else if (downloaderOverride != null)
            {
                _realDownloader = downloaderOverride;
            }

            SchemaRepo = new();
            PriceService = new();
            PngService = new();
            RemoteLookups = new();
            Settings = new();

            PercentSamples = [];
            VisibleToggles = [];
            Steps = [];
        }
        public CardDatabaseManagementService BuildService()
        {
            // 1. Create a unique in-memory DB and keep a reference to dispose later
            var dbName = $"cmtests-{Guid.NewGuid():N}";
            var factory = SharedMemoryDbFactory.CreateInMemoryDbFactory(dbName);
            _dbFactoryDisposable = factory as IDisposable;

            // 2. Set up temp dirs and stubbed settings
            _tmpRoot = Path.Combine(Path.GetTempPath(), "cm-tests", dbName);
            Directory.CreateDirectory(_tmpRoot);

            Settings.Setup(s => s.DatabaseSettings).Returns(new CollectaMundo.ApplicationServices.Shared.DatabaseSettings
            {
                SQLitePath = _tmpRoot
            });
            Settings.Setup(s => s.UserDownloadsPath).Returns(_tmpRoot);
            Settings.Setup(s => s.CardDatabaseUrl).Returns("http://localhost/dummy.sqlite");
            Settings.Setup(s => s.CardPricesUrl).Returns("http://localhost/dummy.json");

            // 3. Create progress sinks (plain Progress<T> objects, no WPF needed)
            var sinks = new ProgressSinks
            {
                Headline = new InlineProgress<string>(_ => { }),
                Detail = new InlineProgress<string>(_ => { }),
                Step = new InlineProgress<string>(s => Steps.Add(s)),
                Percent = new InlineProgress<int>(p => PercentSamples.Add(p)),
                ProgressBarVisible = new InlineProgress<bool>(v => VisibleToggles.Add(v))
            };

            // 4. Inject everything explicitly (no AppGlobals)
            return new CardDatabaseManagementService(
                Settings.Object,
                factory, // <- directly pass the in-memory connection factory here
                sinks,
                SchemaRepo.Object,
                PriceService.Object,
                PngService.Object,
                RemoteLookups.Object,
                CardDatabaseDownloader
            );
        }

        public void StubAllStepsAsSuccess()
        {
            SchemaRepo.Setup(r => r.CreateTablesAsync(It.IsAny<SQLiteConnection>())).Returns(Task.CompletedTask);
            SchemaRepo.Setup(r => r.CreateViewsAsync(It.IsAny<SQLiteConnection>(), It.IsAny<string>())).Returns(Task.CompletedTask);
            SchemaRepo.Setup(r => r.CreateIndicesAsync(It.IsAny<SQLiteConnection>())).Returns(Task.CompletedTask);
            SchemaRepo.Setup(r => r.OptimizeAsync(It.IsAny<SQLiteConnection>())).Returns(Task.CompletedTask);

            PriceService.Setup(p => p.ImportPricesFromJsonAsync(
                    It.IsAny<string>(), It.IsAny<SQLiteConnection>(),
                    It.IsAny<IProgress<string>>(), It.IsAny<IProgress<int>>()))
                .Returns(Task.CompletedTask);

            PngService.Setup(p => p.GenerateMissingManaSymbolImagesAsync(It.IsAny<SQLiteConnection>(), It.IsAny<IProgress<int>>())).Returns(Task.CompletedTask);
            PngService.Setup(p => p.GenerateMissingManaCostImagesAsync(It.IsAny<SQLiteConnection>(), It.IsAny<IProgress<int>>())).Returns(Task.CompletedTask);
            PngService.Setup(p => p.GenerateMissingKeyRuneImagesAsync(It.IsAny<SQLiteConnection>(), It.IsAny<IProgress<int>>())).Returns(Task.CompletedTask);

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
        sealed class InlineProgress<T> : IProgress<T>
        {
            private readonly Action<T> _onReport;
            public InlineProgress(Action<T> onReport) => _onReport = onReport;
            public void Report(T value) => _onReport(value);
        }
    }
}
