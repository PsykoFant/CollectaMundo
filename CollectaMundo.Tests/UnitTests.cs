using CollectaMundo.ApplicationServices;
using CollectaMundo.ApplicationServices.CardPrices;
using CollectaMundo.ApplicationServices.DownloadResourceFiles;
using CollectaMundo.ApplicationServices.GenerateMissingPng;
using CollectaMundo.ApplicationServices.Utilities;
using CollectaMundo.Data;
using CollectaMundo.Data.EditCollection;
using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.EditCollection;
using CollectaMundo.DomainLogic.EditCollection.Models;
using CollectaMundo.DomainLogic.Filtering;
using CollectaMundo.Presentation.Converters;
using CollectaMundo.ViewModels;
using Moq;
using ServiceStack;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Windows.Media.Imaging;
using static CollectaMundo.Tests.TestUtilities;


namespace CollectaMundo.Tests
{
    public class UnitTests
    {
        public class Filtering
        {
            private readonly static List<CardSet> cards = GetTestCards();
            public class FilterByNumericOptionsTests
            {

                [Fact]
                public void Test_NumericFilter_ManaValueGreaterThan3()
                {
                    // Arrange: build the domain filterLogic right here
                    var filterLogic = new FilteringLogic(
                        criteriaKey: "ManaValue",
                        filterCategory: FilterType.Numeric,
                        selectedOptions: [],
                        selectedSingleOption: null,
                        selectedNumericValue: 3,
                        operatorSelection: OperatorType.GREATER_THAN,
                        defaultText: String.Empty
                    );

                    // Act: run it over your test cards
                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();

                    // Assert
                    Assert.All(result, card => Assert.True(card.ManaValue > 3));
                    Assert.Equal(9, result.Count);
                }

                [Fact]
                public void Test_NumericFilter_ManaValueEqual_To_Zero()
                {
                    var filterLogic = new FilteringLogic(
                        criteriaKey: "ManaValue",
                        filterCategory: FilterType.Numeric,
                        selectedOptions: [],
                        selectedSingleOption: null,
                        selectedNumericValue: 0,
                        operatorSelection: OperatorType.EQUALS,
                        defaultText: String.Empty
                    );

                    // Apply the filter using the Matches method.
                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();

                    // Assert something about the resulting list.
                    Assert.True(result.All(card => card.ManaValue == 0));
                    Assert.Equal(3, result.Count);
                }
            }
            public class FilterBySingleOptionTests
            {
                [Fact]
                public void Test_SingleNameContains_Part_Of_Name()
                {
                    var filterLogic = new FilteringLogic(
                        criteriaKey: "Name",
                        filterCategory: FilterType.Single,
                        selectedOptions: [],
                        selectedSingleOption: "fire",
                        selectedNumericValue: 0,
                        operatorSelection: OperatorType.OR,
                        defaultText: String.Empty
                    );

                    // Now filter the list
                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();

                    // Assert that only cards with "Lightning" in their name are returned.
                    Assert.Equal(2, result.Count);
                    Assert.Contains("Fire // Ice", result[0].Name);
                    Assert.Contains("Tarfire", result[1].Name);
                }

                [Fact]
                public void Test_SingleNameContains_Whole_Name()
                {
                    var filterLogic = new FilteringLogic(
                        criteriaKey: "Name",
                        filterCategory: FilterType.Single,
                        selectedOptions: [],
                        selectedSingleOption: "Davros, Dalek Creator",
                        selectedNumericValue: 0,
                        operatorSelection: OperatorType.OR,
                        defaultText: String.Empty
                    );

                    // Now filter the list
                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();

                    // Assert that only cards with "Lightning" in their name are returned.
                    Assert.Single(result);
                    Assert.Contains("Davros, Dalek Creator", result[0].Name);
                }
            }
            public class FilterByMultiOptionsTests
            {

                [Fact]
                public void Test_MultiSelect_OR()
                {
                    var filterLogic = new FilteringLogic(
                        criteriaKey: "Types",
                        filterCategory: FilterType.Multi,
                        selectedOptions: ["Sorcery", "Instant"],
                        selectedSingleOption: null,
                        selectedNumericValue: null,
                        operatorSelection: OperatorType.OR,
                        defaultText: String.Empty
                    );

                    // Filter cards using the Matches method.
                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();
                    Assert.Equal(6, result.Count);
                }

                [Fact]
                public void Test_MultiSelect_AND()
                {
                    var filterLogic = new FilteringLogic(
                        criteriaKey: "Types",
                        filterCategory: FilterType.Multi,
                        selectedOptions: ["Artifact", "Creature"],
                        selectedSingleOption: null,
                        selectedNumericValue: null,
                        operatorSelection: OperatorType.AND,
                        defaultText: String.Empty
                    );

                    // Filter cards using the Matches method.
                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();
                    Assert.Equal(2, result.Count);
                }

                [Fact]
                public void Test_MultiSelect_NOT()
                {
                    var filterLogic = new FilteringLogic(
                        criteriaKey: "Rarity",
                        filterCategory: FilterType.Multi,
                        selectedOptions: ["uncommon", "rare"],
                        selectedSingleOption: null,
                        selectedNumericValue: null,
                        operatorSelection: OperatorType.NOT,
                        defaultText: String.Empty
                    );

                    // Filter cards using the Matches method.
                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();
                    Assert.Equal(8, result.Count);
                }
            }
            public class FilterByColorTests
            {

                [Fact]
                public void Test_SingleColor_OR_Red()
                {
                    var filterLogic = new FilteringLogic(
                        criteriaKey: "Colors",
                        filterCategory: FilterType.Multi,
                        selectedOptions: ["R"],
                        selectedSingleOption: null,
                        selectedNumericValue: null,
                        operatorSelection: OperatorType.OR,
                        defaultText: String.Empty
                    );

                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();

                    Assert.Equal(8, result.Count);
                }

                [Fact]
                public void Test_TwoColors_OR_G_R()
                {
                    var filterLogic = new FilteringLogic(
                        criteriaKey: "Colors",
                        filterCategory: FilterType.Multi,
                        selectedOptions: ["R", "G"],
                        selectedSingleOption: null,
                        selectedNumericValue: null,
                        operatorSelection: OperatorType.OR,
                        defaultText: String.Empty
                    );

                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();

                    Assert.Equal(12, result.Count);
                }

                [Fact]
                public void Test_TwoColors_NOT_W_R()
                {
                    var filterLogic = new FilteringLogic(
                        criteriaKey: "Colors",
                        filterCategory: FilterType.Multi,
                        selectedOptions: ["R", "W"],
                        selectedSingleOption: null,
                        selectedNumericValue: null,
                        operatorSelection: OperatorType.NOT,
                        defaultText: String.Empty
                    );

                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();

                    // Expected: Cards that do NOT have W or R.
                    Assert.Equal(9, result.Count);
                }

                [Fact]
                public void Test_TwoColors_AND_G_U()
                {
                    var filterLogic = new FilteringLogic(
                        criteriaKey: "Colors",
                        filterCategory: FilterType.Multi,
                        selectedOptions: ["G", "U"],
                        selectedSingleOption: null,
                        selectedNumericValue: null,
                        operatorSelection: OperatorType.AND,
                        defaultText: String.Empty
                    );

                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();

                    // Expected: "Biomass Mutation" has Colors = "G, U".
                    Assert.Single(result);
                }
                [Fact]
                public void Test_SingleColor_OR_C()
                {
                    var filterLogic = new FilteringLogic(
                        criteriaKey: "Colors",
                        filterCategory: FilterType.Multi,
                        selectedOptions: ["R", "C"],
                        selectedSingleOption: null,
                        selectedNumericValue: null,
                        operatorSelection: OperatorType.OR,
                        defaultText: String.Empty
                    );

                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();

                    Assert.Equal(9, result.Count);
                }

                [Fact]
                public void Test_NOT_R_NOT_C()
                {
                    var filterLogic = new FilteringLogic(
                        criteriaKey: "Colors",
                        filterCategory: FilterType.Multi,
                        selectedOptions: ["R", "C"],
                        selectedSingleOption: null,
                        selectedNumericValue: null,
                        operatorSelection: OperatorType.NOT,
                        defaultText: String.Empty
                    );

                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();

                    Assert.Equal(9, result.Count);
                }

                [Fact]
                public void Test_SingleColor_AND_X()
                {
                    var filterLogic = new FilteringLogic(
                        criteriaKey: "Colors",
                        filterCategory: FilterType.Multi,
                        selectedOptions: ["B", "X"],
                        selectedSingleOption: null,
                        selectedNumericValue: null,
                        operatorSelection: OperatorType.AND,
                        defaultText: String.Empty
                    );

                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();

                    Assert.Single(result);
                }

                [Fact]
                public void Test_TwoColors_AND_X()
                {
                    var filterLogic = new FilteringLogic(
                        criteriaKey: "Colors",
                        filterCategory: FilterType.Multi,
                        selectedOptions: ["G", "U", "X"],
                        selectedSingleOption: null,
                        selectedNumericValue: null,
                        operatorSelection: OperatorType.AND,
                        defaultText: String.Empty
                    );

                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();

                    Assert.Single(result);
                }

                [Fact]
                public void Test_ThreeColors_AND_X()
                {
                    var filterLogic = new FilteringLogic(
                        criteriaKey: "Colors",
                        filterCategory: FilterType.Multi,
                        selectedOptions: ["G", "U", "B", "X"],
                        selectedSingleOption: null,
                        selectedNumericValue: null,
                        operatorSelection: OperatorType.AND,
                        defaultText: String.Empty
                    );

                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();

                    Assert.Single(result);
                }

                [Fact]
                public void Test_Colorless_OR()
                {
                    var filterLogic = new FilteringLogic(
                        criteriaKey: "Colors",
                        filterCategory: FilterType.Multi,
                        selectedOptions: ["Colorless"],
                        selectedSingleOption: null,
                        selectedNumericValue: null,
                        operatorSelection: OperatorType.OR,
                        defaultText: String.Empty
                    );

                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();

                    Assert.Equal(5, result.Count);
                }

                [Fact]
                public void Test_Colorless_X_NOT()
                {
                    var filterLogic = new FilteringLogic(
                        criteriaKey: "Colors",
                        filterCategory: FilterType.Multi,
                        selectedOptions: ["Colorless", "X"],
                        selectedSingleOption: null,
                        selectedNumericValue: null,
                        operatorSelection: OperatorType.NOT,
                        defaultText: String.Empty
                    );

                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();

                    Assert.Equal(11, result.Count);
                }

                [Fact]
                public void Test_Colorless_AND_C()
                {
                    var filterLogic = new FilteringLogic(
                        criteriaKey: "Colors",
                        filterCategory: FilterType.Multi,
                        selectedOptions: ["Colorless", "C"],
                        selectedSingleOption: null,
                        selectedNumericValue: null,
                        operatorSelection: OperatorType.AND,
                        defaultText: String.Empty
                    );

                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();

                    Assert.Single(result);
                }

                [Fact]
                public void Test_Colorless_AND_R()
                {
                    var filterLogic = new FilteringLogic(
                        criteriaKey: "Colors",
                        filterCategory: FilterType.Multi,
                        selectedOptions: ["Colorless", "R"],
                        selectedSingleOption: null,
                        selectedNumericValue: null,
                        operatorSelection: OperatorType.AND,
                        defaultText: String.Empty
                    );

                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();

                    Assert.Empty(result);
                }

                [Fact]
                public void Test_Colorless_AND_C_AND_X()
                {
                    var filterLogic = new FilteringLogic(
                        criteriaKey: "Colors",
                        filterCategory: FilterType.Multi,
                        selectedOptions: ["Colorless", "C", "X"],
                        selectedSingleOption: null,
                        selectedNumericValue: null,
                        operatorSelection: OperatorType.AND,
                        defaultText: String.Empty
                    );

                    var result = cards.Where(card => filterLogic.Matches(card)).ToList();

                    Assert.Single(result);
                }
            }
        }
        public class Converters
        {
            // CountToSummaryConverter
            [Fact]
            public void Converter_Reflects_ViewModel_Counts()
            {
                // Arrange – populate the view‑model exactly as you already do in other tests
                var vm = new CardViewModel();
                vm.Cards.AddRange(TestUtilities.GetTestCards());

                // pretend the user applied a filter that left 7 cards
                vm.FilteredCards = [.. vm.Cards.Take(7)];

                var converter = new CountToSummaryConverter();

                // Act
                var result = converter.Convert(
                    [vm.FilteredCards.Count, vm.Cards.Count],
                    typeof(string), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.Equal($"Showing 7 cards out of {vm.Cards.Count} cards.", result);
            }

            // StringToImageSourceConverter
            [WpfFact]
            public void Convert_NullOrEmpty_ReturnsNull()
            {
                // arrange
                var converter = new StringToImageSourceConverter();

                // act  (runs on an STA thread)
                var img1 = converter.Convert(null, typeof(BitmapImage), null, CultureInfo.InvariantCulture);
                var img2 = converter.Convert(string.Empty, typeof(BitmapImage), null, CultureInfo.InvariantCulture);

                // assert
                Assert.Null(img1);
                Assert.Null(img2);
            }

            [WpfFact]
            public void Convert_InvalidUri_ReturnsNull()
            {
                var converter = new StringToImageSourceConverter();
                const string bogus = "this-is-not-a-valid-uri";

                var result = converter.Convert(bogus, typeof(BitmapImage), null, CultureInfo.InvariantCulture);

                Assert.Null(result);
            }

            [WpfFact]
            public void Convert_ValidAbsoluteUri_ReturnsBitmapImageWithSameUri()
            {
                var converter = new StringToImageSourceConverter();
                const string url = "https://via.placeholder.com/50";

                var obj = converter.Convert(url, typeof(BitmapImage), null, CultureInfo.InvariantCulture);

                var bmp = Assert.IsType<BitmapImage>(obj);
                Assert.Equal(url, bmp.UriSource!.AbsoluteUri);
            }
        }
        public class EditCollectionLogicTests
        {
            [Fact]
            public async Task SaveBatchAsync_AddNewCard_WhenNotExisting()
            {
                var repo = new Mock<IEditCollectionRepository>();
                var dummyConn = new SQLiteConnection();

                // When we ask “find existing?”, return “no”
                repo.Setup(r => r.FindExistingCardReturnIdAsync(It.IsAny<CardSet>(), It.IsAny<SQLiteConnection>())).ReturnsAsync((int?)null);


                // When we add, return card id 123
                repo.Setup(r => r.AddCardAndReturnIdAsync(It.IsAny<CardSet>(), It.IsAny<SQLiteConnection>())).ReturnsAsync(123);

                var logic = new EditCollectionLogic(repo.Object);

                var newCard = new CardSet
                {
                    Uuid = "foo-uuid",
                    SelectedCondition = "Near Mint",
                    SelectedFinish = "nonfoil",
                    Language = "German",
                    CardsOwned = 2,
                    CardsForTrade = 1
                };

                // Act
                var results = await logic.SaveBatchAsync([newCard], isEdit: false, dummyConn);

                // Assert
                var evt = Assert.Single(results);
                Assert.Equal(CardChangeEventArgs.ChangeType.Upsert, evt.Type);
                Assert.NotNull(evt.Survivor);
                Assert.Equal(123, evt.Survivor.CardId);
                Assert.Equal("foo-uuid", evt.Survivor.Uuid);
                Assert.Equal("Near Mint", evt.Survivor.SelectedCondition);
                Assert.Equal("nonfoil", evt.Survivor.SelectedFinish);
                Assert.Equal("German", evt.Survivor.Language);
                Assert.Equal(2, evt.Survivor.CardsOwned);
                Assert.Equal(1, evt.Survivor.CardsForTrade);

                // verify repo was called
                repo.Verify(r => r.AddCardAndReturnIdAsync(newCard, dummyConn), Times.Once);
            }
            [Fact]
            public async Task SaveBatchAsync_AddNewCard_AddToExisting()
            {
                var repo = new Mock<IEditCollectionRepository>();
                var dummyConn = new SQLiteConnection();

                // When we ask “find existing?”, return card id 123
                repo.Setup(r => r.FindExistingCardReturnIdAsync(It.IsAny<CardSet>(), It.IsAny<SQLiteConnection>())).ReturnsAsync(123);


                // No-op
                repo.Setup(r => r.UpdateCardCountsAsync(It.IsAny<CardSet>(), It.IsAny<SQLiteConnection>())).Returns(Task.CompletedTask);


                // Return somewhat arbitrary owned/trade counts
                repo.Setup(r => r.GetTotalsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SQLiteConnection>())).ReturnsAsync((6, 4));

                var logic = new EditCollectionLogic(repo.Object);

                var newCard = new CardSet
                {
                    Uuid = "foo-uuid",
                    SelectedCondition = "Near Mint",
                    SelectedFinish = "nonfoil",
                    Language = "German",
                    CardsOwned = 2,
                    CardsForTrade = 1
                };

                // Act
                var results = await logic.SaveBatchAsync([newCard], isEdit: false, dummyConn);

                // Assert
                var evt = Assert.Single(results);
                Assert.Equal(CardChangeEventArgs.ChangeType.Upsert, evt.Type);
                Assert.NotNull(evt.Survivor);
                Assert.Equal(123, evt.Survivor.CardId);
                Assert.Equal("foo-uuid", evt.Survivor.Uuid);
                Assert.Equal("Near Mint", evt.Survivor.SelectedCondition);
                Assert.Equal("nonfoil", evt.Survivor.SelectedFinish);
                Assert.Equal("German", evt.Survivor.Language);
                Assert.Equal(6, evt.Survivor.CardsOwned);
                Assert.Equal(4, evt.Survivor.CardsForTrade);

                // verify repo was called
                repo.Verify(r => r.UpdateCardCountsAsync(newCard, dummyConn), Times.Once);
                repo.Verify(r => r.GetTotalsAsync(newCard.Uuid, newCard.SelectedCondition, newCard.Language, newCard.SelectedFinish, dummyConn), Times.Once);

            }

            [Fact]
            public async Task SaveBatchAsync_EditCard_DeleteByZero()
            {
                var repo = new Mock<IEditCollectionRepository>();
                var dummyConn = new SQLiteConnection();

                // Delete Card 
                repo.Setup(r => r.DeleteCardByIdAsync(It.IsAny<CardSet>(), It.IsAny<SQLiteConnection>())).Returns(Task.CompletedTask);
                var logic = new EditCollectionLogic(repo.Object);

                var newCard = new CardSet
                {
                    CardId = 123,
                    Uuid = "foo-uuid",
                    SelectedCondition = "Near Mint",
                    SelectedFinish = "nonfoil",
                    Language = "German",
                    CardsOwned = 0,
                    CardsForTrade = 1
                };

                // Act
                var results = await logic.SaveBatchAsync([newCard], isEdit: true, dummyConn);

                // Assert
                var evt = Assert.Single(results);
                Assert.Equal(CardChangeEventArgs.ChangeType.Delete, evt.Type);

                // verify repo was called
                repo.Verify(r => r.DeleteCardByIdAsync(newCard, dummyConn), Times.Once);

            }

            [Fact]
            public async Task SaveBatchAsync_EditCard_Update_no_merge()
            {
                var repo = new Mock<IEditCollectionRepository>();
                var dummyConn = new SQLiteConnection();

                // Mock update
                repo.Setup(r => r.UpdateCardCountsAsync(It.IsAny<CardSet>(), It.IsAny<SQLiteConnection>())).Returns(Task.CompletedTask);

                // Return a single id
                repo.Setup(r => r.FindRecordByIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SQLiteConnection>())).ReturnsAsync([123]);

                var logic = new EditCollectionLogic(repo.Object);

                var newCard = new CardSet
                {
                    CardId = 123,
                    Uuid = "foo-uuid",
                    SelectedCondition = "Near Mint",
                    SelectedFinish = "nonfoil",
                    Language = "German",
                    CardsOwned = 3,
                    CardsForTrade = 1
                };

                // Act
                var results = await logic.SaveBatchAsync([newCard], isEdit: true, dummyConn);

                // Assert
                var evt = Assert.Single(results);
                Assert.Equal(CardChangeEventArgs.ChangeType.Upsert, evt.Type);
                Assert.NotNull(evt.Survivor);
                Assert.Equal(123, evt.Survivor.CardId);
                Assert.Equal("foo-uuid", evt.Survivor.Uuid);
                Assert.Equal("Near Mint", evt.Survivor.SelectedCondition);
                Assert.Equal("nonfoil", evt.Survivor.SelectedFinish);
                Assert.Equal("German", evt.Survivor.Language);
                Assert.Equal(3, evt.Survivor.CardsOwned);
                Assert.Equal(1, evt.Survivor.CardsForTrade);

                // verify repo was called
                repo.Verify(r => r.UpdateCardAsync(newCard, dummyConn), Times.Once);

                // verify this was NOT called
                repo.Verify(r => r.MergeDuplicateRecordsAsync(newCard.Uuid, newCard.SelectedCondition, newCard.Language, newCard.SelectedFinish, 123, dummyConn), Times.Never);
            }

            [Fact]
            public async Task SaveBatchAsync_EditCard_Update_merge()
            {
                var repo = new Mock<IEditCollectionRepository>();
                var dummyConn = new SQLiteConnection();

                // Mock update
                repo.Setup(r => r.UpdateCardCountsAsync(It.IsAny<CardSet>(), It.IsAny<SQLiteConnection>())).Returns(Task.CompletedTask);

                // Return multiple ids
                repo.Setup(r => r.FindRecordByIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SQLiteConnection>())).ReturnsAsync([123, 456, 789]);

                // Return somewhat arbitrary owned/trade counts
                repo.Setup(r => r.GetTotalsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SQLiteConnection>())).ReturnsAsync((6, 4));


                var logic = new EditCollectionLogic(repo.Object);

                var newCard = new CardSet
                {
                    CardId = 123,
                    Uuid = "foo-uuid",
                    SelectedCondition = "Near Mint",
                    SelectedFinish = "nonfoil",
                    Language = "German",
                    CardsOwned = 3,
                    CardsForTrade = 1
                };

                // Act
                var results = await logic.SaveBatchAsync([newCard], isEdit: true, dummyConn);

                // Assert
                var evt = Assert.Single(results);
                Assert.Equal(CardChangeEventArgs.ChangeType.Upsert, evt.Type);
                Assert.NotNull(evt.Survivor);
                Assert.Equal(123, evt.Survivor.CardId);
                Assert.Equal("foo-uuid", evt.Survivor.Uuid);
                Assert.Equal("Near Mint", evt.Survivor.SelectedCondition);
                Assert.Equal("nonfoil", evt.Survivor.SelectedFinish);
                Assert.Equal("German", evt.Survivor.Language);
                Assert.Equal(6, evt.Survivor.CardsOwned);
                Assert.Equal(4, evt.Survivor.CardsForTrade);
                Assert.Equal([456, 789], evt.Removed);

                // verify repo was called
                repo.Verify(r => r.UpdateCardAsync(newCard, dummyConn), Times.Once);

                // verify this was NOT called
                repo.Verify(r => r.MergeDuplicateRecordsAsync(newCard.Uuid, newCard.SelectedCondition, newCard.Language, newCard.SelectedFinish, 123, dummyConn), Times.Once);
            }
        }
        public class FirstTimeSetupLogicTests
        {
            public class RetryBehavior
            {
                [Fact]
                public async Task FirstTimeDbPrepOrchetrator_RetriesOnStepFailure()
                {
                    // Arrange
                    var mockSchemaRepo = new Mock<IDatabaseSchemaRepository>();
                    var mockPriceService = new Mock<ICardPriceService>();
                    var mockPngService = new Mock<IGenerateMissingPngService>();
                    var mockDownloadService = new Mock<IDownloadService>();
                    var statusVM = new StatusViewModel();
                    var settings = new JsonAppSettings();
                    AppGlobals.DbFactory = new DbConnectionFactory(settings);

                    // Fail first two calls to CreateTablesAsync, succeed on third
                    int callCount = 0;
                    mockSchemaRepo
                        .Setup(r => r.CreateTablesAsync(It.IsAny<SQLiteConnection>()))
                        .Returns(async () =>
                        {
                            callCount++;
                            if (callCount < 3)
                                throw new Exception($"Simulated failure on attempt {callCount}");
                        });

                    // Mock DownloadParallelAsync to return OperationResult.Success
                    mockDownloadService
                        .Setup(d => d.DownloadParallelAsync(
                            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                            It.IsAny<int>(),                                     // retryDelayInMs
                            It.IsAny<IProgress<string>>(),                       // detailProgress
                            It.IsAny<IProgress<int>>(),                          // percentProgress
                            It.IsAny<IProgress<string>>(),                       // stepLabelProgress
                            It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new OperationResult(OperationResultCode.Success, "Download succeeded"));

                    // Settings
                    var fakeSettings = new Mock<IAppSettings>();
                    fakeSettings.Setup(s => s.DatabaseSettings).Returns(new ApplicationServices.DatabaseSettings { SQLitePath = Path.GetTempPath() });
                    fakeSettings.Setup(s => s.UserDownloadsPath).Returns(Path.GetTempPath());
                    fakeSettings.Setup(s => s.CardDatabaseUrl).Returns("http://localhost/dummy.sqlite");
                    fakeSettings.Setup(s => s.CardPricesUrl).Returns("http://localhost/dummy.json");

                    // Stub remaining steps to succeed
                    mockPngService.Setup(p => p.GenerateMissingManaSymbolImagesAsync(It.IsAny<SQLiteConnection>(), It.IsAny<IProgress<int>>())).Returns(Task.CompletedTask);
                    mockPngService.Setup(p => p.GenerateMissingManaCostImagesAsync(It.IsAny<SQLiteConnection>(), It.IsAny<IProgress<int>>())).Returns(Task.CompletedTask);
                    mockPngService.Setup(p => p.GenerateMissingKeyRuneImagesAsync(It.IsAny<SQLiteConnection>(), It.IsAny<IProgress<int>>())).Returns(Task.CompletedTask);
                    mockPriceService.Setup(p => p.ImportPricesFromJsonAsync(It.IsAny<string>(), It.IsAny<SQLiteConnection>(), It.IsAny<IProgress<string>>(), It.IsAny<IProgress<int>>())).Returns(Task.CompletedTask);

                    var service = new CardDatabasePreparationService(
                        fakeSettings.Object,
                        mockSchemaRepo.Object,
                        mockPriceService.Object,
                        mockPngService.Object,
                        mockDownloadService.Object,
                        statusVM
                    );

                    // Act
                    await service.FirstTimeDbPrepOrchetrator(0);

                    // Assert
                    Assert.Equal(3, callCount); // Exactly 2 retries, then success
                }
                [Fact]
                public async Task FirstTimeDbPrepOrchetrator_RetriesOuterLoop_WhenStep2Fails()
                {
                    // Arrange
                    var mockSchemaRepo = new Mock<IDatabaseSchemaRepository>();
                    var mockPriceService = new Mock<ICardPriceService>();
                    var mockPngService = new Mock<IGenerateMissingPngService>();
                    var mockDownloadService = new Mock<IDownloadService>();
                    var statusVM = new StatusViewModel();
                    AppGlobals.DbFactory = new DbConnectionFactory(new JsonAppSettings()); // real DbFactory since we rely on file setup

                    // Fail CreateTablesAsync every time to force 3 outer loop attempts
                    int createCallCount = 0;
                    mockSchemaRepo
                        .Setup(r => r.CreateTablesAsync(It.IsAny<SQLiteConnection>()))
                        .Returns(async () =>
                        {
                            createCallCount++;
                            await Task.Yield(); // ensures it's truly async
                            throw new Exception("Step 2 failure");
                        });


                    // Download always succeeds
                    mockDownloadService
                        .Setup(d => d.DownloadParallelAsync(
                            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                            It.IsAny<int>(), It.IsAny<IProgress<string>>(), It.IsAny<IProgress<int>>(), It.IsAny<IProgress<string>>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new OperationResult(OperationResultCode.Success, "Download succeeded"));

                    var fakeSettings = new Mock<IAppSettings>();
                    fakeSettings.Setup(s => s.DatabaseSettings).Returns(new ApplicationServices.DatabaseSettings { SQLitePath = Path.GetTempPath() });
                    fakeSettings.Setup(s => s.UserDownloadsPath).Returns(Path.GetTempPath());
                    fakeSettings.Setup(s => s.CardDatabaseUrl).Returns("http://localhost/dummy.sqlite");
                    fakeSettings.Setup(s => s.CardPricesUrl).Returns("http://localhost/dummy.json");

                    // Stub all other steps as no-op
                    mockPngService.SetupAllProperties();
                    mockPngService.Setup(p => p.GenerateMissingManaSymbolImagesAsync(It.IsAny<SQLiteConnection>(), It.IsAny<IProgress<int>>())).Returns(Task.CompletedTask);
                    mockPngService.Setup(p => p.GenerateMissingManaCostImagesAsync(It.IsAny<SQLiteConnection>(), It.IsAny<IProgress<int>>())).Returns(Task.CompletedTask);
                    mockPngService.Setup(p => p.GenerateMissingKeyRuneImagesAsync(It.IsAny<SQLiteConnection>(), It.IsAny<IProgress<int>>())).Returns(Task.CompletedTask);
                    mockPriceService.Setup(p => p.ImportPricesFromJsonAsync(It.IsAny<string>(), It.IsAny<SQLiteConnection>(), It.IsAny<IProgress<string>>(), It.IsAny<IProgress<int>>())).Returns(Task.CompletedTask);

                    var service = new CardDatabasePreparationService(
                        fakeSettings.Object,
                        mockSchemaRepo.Object,
                        mockPriceService.Object,
                        mockPngService.Object,
                        mockDownloadService.Object,
                        statusVM,
                        shutdownApp: () => { /* noop */ }
                    );

                    // Act
                    await service.FirstTimeDbPrepOrchetrator(0);

                    // Assert
                    // Should have retried the outer loop 3 times; each includes 3 inner attempts = 9 total CreateTablesAsync calls
                    Assert.Equal(9, createCallCount);
                }
                [Fact]
                public async Task FirstTimeDbPrepOrchetrator_RetriesOuterLoop_WhenDownloadFails()
                {
                    // Arrange
                    var mockSchemaRepo = new Mock<IDatabaseSchemaRepository>();
                    var mockPriceService = new Mock<ICardPriceService>();
                    var mockPngService = new Mock<IGenerateMissingPngService>();
                    var mockDownloadService = new Mock<IDownloadService>();
                    var statusVM = new StatusViewModel();
                    AppGlobals.DbFactory = new DbConnectionFactory(new JsonAppSettings());

                    int downloadAttempts = 0;
                    mockDownloadService
                        .Setup(d => d.DownloadParallelAsync(
                            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                            It.IsAny<int>(), It.IsAny<IProgress<string>>(), It.IsAny<IProgress<int>>(), It.IsAny<IProgress<string>>(),
                            It.IsAny<CancellationToken>()))
                        .ReturnsAsync(() =>
                        {
                            downloadAttempts++;
                            if (downloadAttempts < 3)
                                return new OperationResult(OperationResultCode.Error, "Download failed");
                            return new OperationResult(OperationResultCode.Success, "Download succeeded");
                        });

                    var fakeSettings = new Mock<IAppSettings>();
                    fakeSettings.Setup(s => s.DatabaseSettings).Returns(new ApplicationServices.DatabaseSettings { SQLitePath = Path.GetTempPath() });
                    fakeSettings.Setup(s => s.UserDownloadsPath).Returns(Path.GetTempPath());
                    fakeSettings.Setup(s => s.CardDatabaseUrl).Returns("http://localhost/dummy.sqlite");
                    fakeSettings.Setup(s => s.CardPricesUrl).Returns("http://localhost/dummy.json");

                    // Stub all other steps
                    mockSchemaRepo.Setup(r => r.CreateTablesAsync(It.IsAny<SQLiteConnection>())).Returns(Task.CompletedTask);
                    mockSchemaRepo.Setup(r => r.CreateViewsAsync(It.IsAny<SQLiteConnection>(), It.IsAny<string>())).Returns(Task.CompletedTask);
                    mockSchemaRepo.Setup(r => r.CreateIndicesAsync(It.IsAny<SQLiteConnection>())).Returns(Task.CompletedTask);
                    mockSchemaRepo.Setup(r => r.OptimizeAsync(It.IsAny<SQLiteConnection>())).Returns(Task.CompletedTask);
                    mockPriceService.Setup(p => p.ImportPricesFromJsonAsync(It.IsAny<string>(), It.IsAny<SQLiteConnection>(), It.IsAny<IProgress<string>>(), It.IsAny<IProgress<int>>())).Returns(Task.CompletedTask);
                    mockPngService.Setup(p => p.GenerateMissingManaSymbolImagesAsync(It.IsAny<SQLiteConnection>(), It.IsAny<IProgress<int>>())).Returns(Task.CompletedTask);
                    mockPngService.Setup(p => p.GenerateMissingManaCostImagesAsync(It.IsAny<SQLiteConnection>(), It.IsAny<IProgress<int>>())).Returns(Task.CompletedTask);
                    mockPngService.Setup(p => p.GenerateMissingKeyRuneImagesAsync(It.IsAny<SQLiteConnection>(), It.IsAny<IProgress<int>>())).Returns(Task.CompletedTask);

                    var service = new CardDatabasePreparationService(
                        fakeSettings.Object,
                        mockSchemaRepo.Object,
                        mockPriceService.Object,
                        mockPngService.Object,
                        mockDownloadService.Object,
                        statusVM,
                        shutdownApp: () => { /* noop */ }
                    );

                    // Act
                    await service.FirstTimeDbPrepOrchetrator(0); // set retry delay to 0 for fast test

                    // Assert
                    Assert.Equal(3, downloadAttempts);
                }
            }
            public class ShutdownLogic
            {
                [Fact]
                public async Task ExhaustsAllOuterRetries_CallsShutdown()
                {
                    // Arrange
                    var shutdownCalled = false;

                    var mockSchemaRepo = new Mock<IDatabaseSchemaRepository>();
                    var mockPriceService = new Mock<ICardPriceService>();
                    var mockPngService = new Mock<IGenerateMissingPngService>();
                    var mockDownloadService = new Mock<IDownloadService>();
                    var statusVM = new StatusViewModel();
                    AppGlobals.DbFactory = new DbConnectionFactory(new JsonAppSettings());

                    // Download always fails
                    mockDownloadService
                        .Setup(d => d.DownloadParallelAsync(
                            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                            It.IsAny<int>(), It.IsAny<IProgress<string>>(), It.IsAny<IProgress<int>>(), It.IsAny<IProgress<string>>(),
                            It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new OperationResult(OperationResultCode.Error, "fail"));

                    var fakeSettings = new Mock<IAppSettings>();
                    fakeSettings.Setup(s => s.DatabaseSettings).Returns(new ApplicationServices.DatabaseSettings { SQLitePath = Path.GetTempPath() });
                    fakeSettings.Setup(s => s.UserDownloadsPath).Returns(Path.GetTempPath());
                    fakeSettings.Setup(s => s.CardDatabaseUrl).Returns("http://localhost/dummy.sqlite");
                    fakeSettings.Setup(s => s.CardPricesUrl).Returns("http://localhost/dummy.json");

                    var service = new CardDatabasePreparationService(
                        fakeSettings.Object,
                        mockSchemaRepo.Object,
                        mockPriceService.Object,
                        mockPngService.Object,
                        mockDownloadService.Object,
                        statusVM,
                        shutdownApp: () => shutdownCalled = true
                    );

                    // Act
                    await service.FirstTimeDbPrepOrchetrator(0);

                    // Assert
                    Assert.True(shutdownCalled);
                }


                //[Fact] public async Task NoInternet_CallsShutdownImmediately() { ... }
            }

        }
    }
}
