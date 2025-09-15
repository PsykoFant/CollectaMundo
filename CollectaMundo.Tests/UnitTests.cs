using CollectaMundo.ApplicationServices.Utilities;
using CollectaMundo.Data.CardDatabaseManagement;
using CollectaMundo.Data.EditCollection;
using CollectaMundo.DomainLogic.CardLists;
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
using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Media.Imaging;
using static CollectaMundo.Tests.TestUtilities;


namespace CollectaMundo.Tests
{
    public class UnitTests
    {
        public class Filtering
        {
            private readonly static List<CardSet> cards = GetTestCards();

            static Filtering()
            {
                // Ensure SetMetaProvider is populated for these tests
                TestUtilities.SeedSetMetaForTests(cards);
            }
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
        public class FirstTimeSetupAndUpdateLogicTests
        {

            [Fact]
            public async Task FirstTimeDbPrepOrchetrator_AllStepsSucceed_ReturnsSuccess_AndProgressFinishes()
            {
                using var ctx = new FirstTimeSetupTestContext();
                ctx.RemoteLookups.Setup(r => r.IsInternetAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
                ctx.StubAllStepsAsSuccess();

                ctx.CardDatabaseDownloader
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

                var result = await svc.FirstTimeDbPrepOrchetrator(0);

                Assert.Equal(OperationResultCode.Success, result.Code);

                // repo/service calls
                ctx.SchemaRepo.Verify(r => r.CreateTablesAsync(It.IsAny<SQLiteConnection>()), Times.Once);

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
                   .Setup(r => r.CreateTablesAsync(It.IsAny<SQLiteConnection>()))
                   .Returns(async () =>
                   {
                       await Task.Yield();
                       attempts++;
                       if (attempts < 3)
                       {
                           throw new Exception("boom");
                       }
                   });

                ctx.CardDatabaseDownloader
                    .Setup(d => d.DownloadParallelAsync(
                        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                        It.IsAny<int>(), It.IsAny<string>(),
                        It.IsAny<IProgress<string>>(), It.IsAny<IProgress<string>>(),
                        It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new OperationResult(OperationResultCode.Success, "OK"));

                var svc = ctx.BuildService();

                var result = await svc.FirstTimeDbPrepOrchetrator(0);

                Assert.Equal(OperationResultCode.Success, result.Code);
                Assert.Equal(3, attempts); // 2 failures + 1 success
            }

            [Fact]
            public async Task Step2FailsAfterRetries_ReturnsError_AndStopsPipeline()
            {
                using var ctx = new FirstTimeSetupTestContext();
                ctx.RemoteLookups.Setup(r => r.IsInternetAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
                ctx.StubAllStepsAsSuccess();

                ctx.CardDatabaseDownloader
                    .Setup(d => d.DownloadParallelAsync(
                        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                        It.IsAny<int>(), It.IsAny<string>(),
                        It.IsAny<IProgress<string>>(), It.IsAny<IProgress<string>>(),
                        It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new OperationResult(OperationResultCode.Success, "OK"));

                int createCalls = 0;
                ctx.SchemaRepo.Setup(r => r.CreateTablesAsync(It.IsAny<SQLiteConnection>())).Returns(async () => { createCalls++; await Task.Yield(); throw new Exception("Step 2 fails"); });

                var svc = ctx.BuildService();

                var result = await svc.FirstTimeDbPrepOrchetrator(0);

                Assert.Equal(OperationResultCode.Error, result.Code);
                Assert.Equal(3, createCalls); // max retries
                ctx.SchemaRepo.Verify(r => r.CreateViewsAsync(It.IsAny<SQLiteConnection>(), It.IsAny<string>()), Times.Never);
            }

            [Fact]
            public async Task DownloadFails_ReturnsDownloadFailed_DoesNotRunSteps()
            {
                using var ctx = new FirstTimeSetupTestContext();
                ctx.RemoteLookups.Setup(r => r.IsInternetAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
                ctx.StubAllStepsAsSuccess();

                ctx.CardDatabaseDownloader
                    .Setup(d => d.DownloadParallelAsync(
                        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                        It.IsAny<int>(), It.IsAny<string>(),
                        It.IsAny<IProgress<string>>(), It.IsAny<IProgress<string>>(),
                        It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new OperationResult(OperationResultCode.DownloadFailed, "net fail"));

                var svc = ctx.BuildService();

                var result = await svc.FirstTimeDbPrepOrchetrator(0);

                Assert.Equal(OperationResultCode.DownloadFailed, result.Code);
                ctx.SchemaRepo.Verify(r => r.CreateTablesAsync(It.IsAny<SQLiteConnection>()), Times.Never);
                ctx.CardDatabaseDownloader.Verify(d => d.DownloadParallelAsync(
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
                var result = await svc.FirstTimeDbPrepOrchetrator(0);

                Assert.Equal(OperationResultCode.NoInternet, result.Code);
                ctx.CardDatabaseDownloader.VerifyNoOtherCalls();
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

                ctx.CardDatabaseDownloader
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

                ctx.CardDatabaseDownloader
                .Setup(d => d.DownloadParallelAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<int>(), It.IsAny<string>(),
                    It.IsAny<IProgress<string>>(), It.IsAny<IProgress<string>>(),
                    It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OperationResult(OperationResultCode.DownloadFailed, "boom"));


                var svc = ctx.BuildService();
                var result = await svc.FirstTimeDbPrepOrchetrator(0);

                Assert.Equal(OperationResultCode.DownloadFailed, result.Code); // Gracefully mapped
            }
            [Fact]
            public async Task UpdateDbPrepOrchetrator_ProgressReportsBeforeCancel_AreCaptured()
            {
                using var ctx = new FirstTimeSetupTestContext();
                ctx.RemoteLookups.Setup(r => r.IsInternetAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
                ctx.StubAllStepsAsSuccess();

                var cts = new CancellationTokenSource();

                ctx.CardDatabaseDownloader
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
                ctx.CardDatabaseDownloader
                    .Setup(d => d.DownloadParallelAsync(
                        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                        It.IsAny<int>(), It.IsAny<string>(),
                        It.IsAny<IProgress<string>>(), It.IsAny<IProgress<string>>(),
                        It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
                    .Returns(async () =>
                    {
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
                ctx.CardDatabaseDownloader
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


        }
        public class UpdateDbControlFlowLogicTests
        {
            [Fact]
            public async Task UpdateDbAsync_BackupSucceeds_UpdateSucceeds()
            {
                // Arrange
                var (updateVM, statusVM, dbService) = CreateTestableUpdateViewModel(
                    backupResult: new OperationResult(OperationResultCode.Success, "mock-backup-path"),
                    updateResult: new OperationResult(OperationResultCode.Success, "Update complete"));

                // Act: start the update
                updateVM.UpdateDBCommand.Execute(null);

                // Wait for "Go for it!" prompt
                while (statusVM.PrimaryButtonText != "   Go for it!   ")
                {
                    await Task.Delay(1);
                }

                // Simulate user pressing the button
                statusVM.PrimaryButtonCommand.Execute(null);

                // Wait for actual task to complete
                await updateVM.InternalUpdateTask!;

                // Assert
                Assert.Equal("Database updated successfully!", statusVM.StatusLabel1);
                Assert.Equal("Your collection was backed up at mock-backup-path!", statusVM.StatusLabel3);
                Assert.Equal("  OK  ", statusVM.PrimaryButtonText);
                Assert.Equal(Visibility.Visible, statusVM.PrimaryButtonVisibility);
            }
            [Fact]
            public async Task UpdateDbAsync_BackupSucceeds_UpdateCancelledByUser()
            {
                // Arrange
                var (updateVM, statusVM, dbService) = CreateTestableUpdateViewModel(
                    backupResult: new OperationResult(OperationResultCode.Success, "mock-backup-path"),
                    updateResult: new OperationResult(OperationResultCode.CancelledByUser, "Update cancelled by user"));

                // Act: Start the command (this internally calls UpdateDBAsync and captures the task)
                // Start the command (this internally calls UpdateDBAsync and captures the task)
                updateVM.UpdateDBCommand.Execute(null);

                // Wait until the UpdateDBAsync method is actually running
                while (updateVM.InternalUpdateTask is null)
                {
                    await Task.Delay(10);
                }

                // Simulate user cancel
                SimulatePrimaryButtonClick(statusVM);

                // Now wait for UpdateDBAsync to complete
                await updateVM.InternalUpdateTask!;

                // Assert
                Assert.Equal("Update canceled", statusVM.StatusLabel1);

                Assert.Equal("Download aborted. No files were imported.", statusVM.StatusLabel3);
                Assert.Equal("  OK  ", statusVM.PrimaryButtonText);
                Assert.Equal(Visibility.Visible, statusVM.PrimaryButtonVisibility);
            }
            [Fact]
            public async Task UpdateDbAsync_BackupSucceeds_UpdateFails()
            {
                // Arrange
                var (updateVM, statusVM, dbService) = CreateTestableUpdateViewModel(
                    backupResult: new OperationResult(OperationResultCode.Success, "mock-backup-path"),
                    updateResult: new OperationResult(OperationResultCode.Error, "Boom!"));

                // Act: start the update
                updateVM.UpdateDBCommand.Execute(null);

                // Wait for "Go for it!" prompt
                while (statusVM.PrimaryButtonText != "   Go for it!   ")
                {
                    await Task.Delay(1);
                }

                // Simulate user pressing the button
                statusVM.PrimaryButtonCommand.Execute(null);

                // Wait for actual task to complete
                await updateVM.InternalUpdateTask!;

                // Assert
                Assert.Equal("Card database update failed!", statusVM.StatusLabel1);

                Assert.Equal("Boom!", statusVM.StatusLabel3);
                Assert.Equal("  OK  ", statusVM.PrimaryButtonText);
                Assert.Equal(Visibility.Visible, statusVM.PrimaryButtonVisibility);
            }
            [Fact]
            public async Task UpdateDbAsync_BackupFails_UpdateNotInvoked()
            {
                // Arrange
                var (updateVM, statusVM, dbService) = CreateTestableUpdateViewModel(
                    backupResult: new OperationResult(OperationResultCode.Error, "Backup Boom!"),
                    updateResult: null, // Won’t be used since update should not run
                    getMyCollectionCount: () => 5
                );

                // Act
                updateVM.UpdateDBCommand.Execute(null);

                // Wait for prompt
                while (statusVM.PrimaryButtonText != "   Go for it!   ")
                {
                    await Task.Delay(1);
                }

                // Simulate user clicking the button
                SimulatePrimaryButtonClick(statusVM);

                // Wait for task to complete
                await updateVM.InternalUpdateTask!;

                // Assert
                Assert.Equal("Backup failed - aborting update...", statusVM.StatusLabel1);
                Assert.Equal("Backup Boom!", statusVM.StatusLabel3);
                Assert.Equal("  OK  ", statusVM.PrimaryButtonText);
                Assert.Equal(Visibility.Visible, statusVM.PrimaryButtonVisibility);

                // Verify update was not invoked
                dbService.Verify(s => s.UpdateDbPrepOrchetrator(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
            }
            [Fact]
            public async Task UpdateDbAsync_BackupCancelled_UpdateNotInvoked()
            {
                // Arrange
                var (updateVM, statusVM, dbService) = CreateTestableUpdateViewModel(
                    backupResult: new OperationResult(OperationResultCode.CancelledByUser, "Update was cancelled by user during download."),
                    updateResult: null, // Won’t be used since update should not run
                    getMyCollectionCount: () => 5
                );

                // Act
                updateVM.UpdateDBCommand.Execute(null);

                // Wait for prompt
                while (statusVM.PrimaryButtonText != "   Go for it!   ")
                {
                    await Task.Delay(1);
                }

                // Simulate user clicking the button
                SimulatePrimaryButtonClick(statusVM);

                // Wait for task to complete
                await updateVM.InternalUpdateTask!;

                // Assert
                Assert.Equal("Backup cancelled - aborting update...", statusVM.StatusLabel1);
                Assert.Equal("Update was cancelled by user during download.", statusVM.StatusLabel3);
                Assert.Equal("  OK  ", statusVM.PrimaryButtonText);
                Assert.Equal(Visibility.Visible, statusVM.PrimaryButtonVisibility);

                // Verify update was not invoked
                dbService.Verify(s => s.UpdateDbPrepOrchetrator(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
            }
            [Fact]
            public async Task UpdateDbAsync_EmptyCollection_BackupSkipped_UpdateSucceeds()
            {
                // Arrange
                var (updateVM, statusVM, dbService) = CreateTestableUpdateViewModel(
                    updateResult: new OperationResult(OperationResultCode.Success, "Update complete"),
                    getMyCollectionCount: () => 0 // triggers backup skip
                );

                // Act
                updateVM.UpdateDBCommand.Execute(null);
                while (statusVM.PrimaryButtonText != "   Go for it!   ")
                {
                    await Task.Delay(1);
                }

                statusVM.PrimaryButtonCommand.Execute(null);
                await updateVM.InternalUpdateTask!;

                // Assert
                Assert.Equal("Database updated successfully!", statusVM.StatusLabel1);
                Assert.DoesNotContain("backed up", statusVM.StatusLabel3);
                dbService.Verify(s => s.ExportCollectionAsync(It.IsAny<CancellationToken>()), Times.Never);
            }
            [Fact]
            public async Task UpdateDbAsync_EmptyCollection_BackupSkipped_UpdateCancelledByUser()
            {
                // Arrange
                var (updateVM, statusVM, dbService) = CreateTestableUpdateViewModel(
                    updateResult: new OperationResult(OperationResultCode.CancelledByUser, "Update cancelled by user"),
                    getMyCollectionCount: () => 0 // triggers backup skip
                );

                // Act: Start the command (this internally calls UpdateDBAsync and captures the task)
                // Start the command (this internally calls UpdateDBAsync and captures the task)
                updateVM.UpdateDBCommand.Execute(null);

                // Wait until the UpdateDBAsync method is actually running
                while (updateVM.InternalUpdateTask is null)
                {
                    await Task.Delay(10);
                }

                // Simulate user cancel
                SimulatePrimaryButtonClick(statusVM);

                // Now wait for UpdateDBAsync to complete
                await updateVM.InternalUpdateTask!;

                // Assert
                Assert.Equal("Update canceled", statusVM.StatusLabel1);

                Assert.Equal("Download aborted. No files were imported.", statusVM.StatusLabel3);
                Assert.Equal("  OK  ", statusVM.PrimaryButtonText);
                Assert.Equal(Visibility.Visible, statusVM.PrimaryButtonVisibility);
                dbService.Verify(s => s.ExportCollectionAsync(It.IsAny<CancellationToken>()), Times.Never);
            }
        }
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
                    ConnectCallback = (_, _) =>
                        new ValueTask<Stream>(Task.FromException<Stream>(
                            new HttpRequestException("Simulated no internet connection")))
                };

                var httpClient = new HttpClient(handler);

                var noopStepProgress = new Progress<string>(_ => { });
                var noopDetailProgress = new Progress<string>(_ => { });

                var downloader = new CardDatabaseDownloader(httpClient); // injected

                // Act
                var result = await downloader.DownloadAsync(
                    url: "http://fake-url", // doesn't matter, won't be used
                    targetPath: Path.GetTempFileName(),
                    label: "Test No Internet",
                    retryDelayInMs: 0,
                    stepNameAndNumberProgress: noopStepProgress,
                    stepDetailAndErrorProgress: noopDetailProgress,
                    cancelToken: CancellationToken.None
                );

                // Assert
                Assert.Equal(OperationResultCode.Error, result.Code);
                Assert.Contains("failed", result.Message, StringComparison.OrdinalIgnoreCase);
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
        }
        public class CardCoreAggregatorTests
        {
            private readonly CardCoreAggregator _aggregator = new();

            [Fact]
            public void Aggregates_SingleCard_NoMergeRequired()
            {
                var input = new List<CardCore>
                {
                    new()
                    {
                        Uuid = "card1",
                        Name = "Test Card",
                        Keywords = "Flying",
                        Colors = "W",
                        Types = "Creature",
                        Text = "Some ability",
                        Side = "a"
                    }
                };

                var result = _aggregator.Aggregate(input);

                Assert.Single(result);
                var card = result[0];
                Assert.Equal("Flying", card.Keywords);
                Assert.Equal("W", card.Colors);
                Assert.Equal("Creature", card.Types);
                Assert.Equal("Some ability", card.Text);
            }

            [Fact]
            public void Aggregates_MultiFaceCards_MergesCorrectly()
            {
                var input = new List<CardCore>
                {
                    new()
                    {
                        Uuid = "front",
                        Name = "Front Face",
                        Keywords = "Flying, Haste",
                        Colors = "W, R",
                        Types = "Creature",
                        Text = "Front text",
                        Side = "a",
                        OtherFaceIds = ["back"]
                    },
                    new()
                    {
                        Uuid = "back",
                        Name = "Back Face",
                        Keywords = "Haste, Trample",
                        Colors = "R, G",
                        Types = "Artifact",
                        Text = "Back text",
                        Side = "b"
                    }
                };

                var result = _aggregator.Aggregate(input);

                Assert.Single(result);
                var card = result[0];
                Assert.Equal("W,R,G", card.Colors); // deduplicated & joined
                Assert.Equal("Flying,Haste,Trample", card.Keywords); // deduplicated
                Assert.Contains("Creature", card.Types);
                Assert.Contains("Artifact", card.Types);
                Assert.Equal("Front text // Back text", card.Text);
            }

            [Fact]
            public void Ignores_NonPrimaryFaces_InOutput()
            {
                var input = new List<CardCore>
                {
                    new()
                    {
                        Uuid = "back",
                        Side = "b",
                        Name = "Back Only"
                    }
                };

                var result = _aggregator.Aggregate(input);

                Assert.Empty(result);
            }

            [Fact]
            public void Deduplication_Is_CaseInsensitive()
            {
                var input = new List<CardCore>
                {
                    new()
                    {
                        Name = "Test Card",
                        Uuid = "card1",
                        Side = "a",
                        Keywords = "Flying, flying, FLYING",
                        Colors = "W, w, W",
                        Types = "Creature, creature"
                    }
                };

                var result = _aggregator.Aggregate(input);
                Assert.Single(result);

                var card = result[0];
                Assert.Equal("Flying", card.Keywords);
                Assert.Equal("W", card.Colors);
                Assert.Equal("Creature", card.Types);
            }

            [Fact]
            public void Handles_MissingTextOrKeywordsOrColors_Gracefully()
            {
                var input = new List<CardCore>
                {
                    new()
                    {
                        Name = "Test Card1",
                        Uuid = "card1",
                        Side = "a",
                        Text = null,
                        Keywords = null,
                        Colors = null
                    },
                    new()
                    {
                        Name = "Test Card2",
                        Uuid = "card2",
                        Side = "a",
                        Text = "",
                        Keywords = "",
                        Colors = ""
                    }
                };

                var result = _aggregator.Aggregate(input);
                Assert.Equal(2, result.Count);
            }
        }
    }
}
