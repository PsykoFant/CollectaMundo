using CollectaMundo.ApplicationServices.CardImages;
using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.CardImages;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.Infrastructure.CardImages;
using CollectaMundo.Infrastructure.RemoteLookups;
using CollectaMundo.Tests.TestUtils;
using Moq;
using System.Data.SQLite;
using System.IO;
using System.Net.Http;

namespace CollectaMundo.Tests.UnitTests
{
    public class CardImageTests
    {
        [Fact]
        public async Task GetImageForCardAsync_ReturnsNull_WhenUuidAndNameAreMissing()
        {
            var svc = BuildService(); // uses default mocks
            var card = new CardSet(); // no uuid or name

            var result = await svc.GetImageForCardAsync(card);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetImageForCardAsync_UsesOtherFaceUrl_WhenBackUrlInvalid()
        {
            // Arrange: use a persistent in-memory DB
            var dbName = $"TestDb_{Guid.NewGuid()}";
            var dbFactory = SharedMemoryDbFactory.CreateInMemoryDbFactory(dbName);
            var uowRunner = new UnitOfWorkRunner(dbFactory);

            // Prepare the card
            var card = new CardSet { Uuid = "abc", Side = "a", Name = "Foo" };

            // Mock repository so we don’t care about actual DB contents
            var repo = new Mock<ICardImageRepo>();
            repo.Setup(r => r.GetScryfallIdByUuidAsync("abc", It.IsAny<SQLiteConnection>()))
                .ReturnsAsync("abcdef");
            repo.Setup(r => r.GetOtherFaceScryfallIdByUuidAsync("abc", It.IsAny<SQLiteConnection>()))
                .ReturnsAsync("xyz123");
            repo.Setup(r => r.GetImagePromoTypeByUuidAsync("abc", It.IsAny<SQLiteConnection>()))
                .ReturnsAsync((string?)null);

            // CardImageLogic just builds URLs
            var logic = new CardImageLogic();

            // Mock remote lookups so back URL appears invalid
            var remoteLookups = new Mock<IRemoteLookups>();
            remoteLookups.Setup(r => r.IsValidUrlAsync(It.IsAny<string>()))
                .ReturnsAsync(false); // Force invalid back URL

            // Mock downloader to return dummy bytes
            var downloader = new Mock<ICardImageDownloader>();
            downloader.Setup(d => d.DownloadAsync(It.IsAny<string>(), "abc", "front"))
                      .ReturnsAsync([1, 2, 3]);
            downloader.Setup(d => d.DownloadAsync(It.IsAny<string>(), "abc", "back"))
                      .ReturnsAsync([4, 5, 6]);

            // CreateCollectionChangeSetFromEdits the service with our mocks + real in-memory DB factory
            var service = new CardImageService(uowRunner, remoteLookups.Object, logic, repo.Object, downloader.Object);

            // Act
            var result = await service.GetImageForCardAsync(card);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(new byte[] { 1, 2, 3 }, result!.FrontImageBytes);
            Assert.Equal(new byte[] { 4, 5, 6 }, result.BackImageBytes);
        }

        [Fact]
        public async Task DownloadAsync_ReturnsCachedImage_IfFileExists()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            var uuid = "some-uuid";
            var side = "front";
            var expectedBytes = new byte[] { 99, 88, 77 };
            var filePath = Path.Combine(tempDir, $"{uuid}_{side}_normal.jpg");
            await File.WriteAllBytesAsync(filePath, expectedBytes);

            var settings = new Mock<IAppSettings>();
            settings.Setup(s => s.CardImageCachePath).Returns(tempDir);

            var downloader = new CardImageDownloader(settings.Object);

            var result = await downloader.DownloadAsync("https://irrelevant.com/image.jpg", uuid, side);

            Assert.Equal(expectedBytes, result);
        }

        [Fact]
        public async Task DownloadAsync_FallsBackToHttp_WhenDiskReadFails()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            var uuid = "test-uuid";
            var side = "front";
            var filePath = Path.Combine(tempDir, $"{uuid}_{side}_normal.jpg");

            // Corrupt the file (open in exclusive mode to simulate locked file)
            using var stream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

            var settings = new Mock<IAppSettings>();
            settings.Setup(s => s.CardImageCachePath).Returns(tempDir);

            var fakeHttp = FakeHttpMessageHandler.WithStaticResponse("hello-world-image");
            var downloader = new CardImageDownloader(settings.Object);
            typeof(CardImageDownloader)
                .GetField("_httpClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .SetValue(downloader, fakeHttp);

            var result = await downloader.DownloadAsync("https://irrelevant.com/image.jpg", uuid, side);

            Assert.NotNull(result);
            Assert.Contains((byte)'h', result!); // crude check
        }

        [Fact]
        public async Task DownloadAsync_ReturnsNull_WhenHttpThrows()
        {
            var settings = new Mock<IAppSettings>();
            settings.Setup(s => s.CardImageCachePath).Returns(Path.GetTempPath());

            var downloader = new CardImageDownloader(settings.Object);
            typeof(CardImageDownloader)
                .GetField("_httpClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .SetValue(downloader, FakeHttpMessageHandler.WithException(new HttpRequestException("test")));

            var result = await downloader.DownloadAsync("https://whatever.com/image.jpg", "id123", "front");

            Assert.Null(result);
        }
        private static CardImageService BuildService(IUnitOfWorkRunner? uowRunner = null, IRemoteLookups? remoteLookups = null, ICardImageLogic? logic = null, ICardImageRepo? repo = null, ICardImageDownloader? downloader = null)
        {
            return new CardImageService(
                uowRunner ?? Mock.Of<IUnitOfWorkRunner>(),
                remoteLookups ?? Mock.Of<IRemoteLookups>(),
                logic ?? new CardImageLogic(),
                repo ?? Mock.Of<ICardImageRepo>(),
                downloader ?? Mock.Of<ICardImageDownloader>()
            );
        }


    }
}
