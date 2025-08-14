using CollectaMundo.ApplicationServices.Utilities;
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

            [Fact]
            public async Task FirstTimeDbPrepOrchetrator_AllStepsSucceed_ReturnsSuccess_AndProgressFinishes()
            {
                using var ctx = new FirstTimeSetupTestContext();
                ctx.RemoteLookups.Setup(r => r.IsInternetAvailableAsync()).ReturnsAsync(true);
                ctx.StubAllStepsAsSuccess();

                ctx.DownloadService
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
                ctx.RemoteLookups.Setup(r => r.IsInternetAvailableAsync()).ReturnsAsync(true);
                ctx.StubAllStepsAsSuccess();

                int attempts = 0;
                ctx.SchemaRepo
                   .Setup(r => r.CreateTablesAsync(It.IsAny<SQLiteConnection>()))
                   .Returns(async () =>
                   {
                       await Task.Yield();
                       attempts++;
                       if (attempts < 3) throw new Exception("boom");
                   });

                ctx.DownloadService
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
                ctx.RemoteLookups.Setup(r => r.IsInternetAvailableAsync()).ReturnsAsync(true);
                ctx.StubAllStepsAsSuccess();

                ctx.DownloadService
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
                ctx.RemoteLookups.Setup(r => r.IsInternetAvailableAsync()).ReturnsAsync(true);
                ctx.StubAllStepsAsSuccess();

                ctx.DownloadService
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
                ctx.DownloadService.Verify(d => d.DownloadParallelAsync(
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
                ctx.RemoteLookups.Setup(r => r.IsInternetAvailableAsync()).ReturnsAsync(false);

                var svc = ctx.BuildService();
                var result = await svc.FirstTimeDbPrepOrchetrator(0);

                Assert.Equal(OperationResultCode.NoInternet, result.Code);
                ctx.DownloadService.VerifyNoOtherCalls();
                ctx.SchemaRepo.VerifyNoOtherCalls();
            }

        }
    }
}
