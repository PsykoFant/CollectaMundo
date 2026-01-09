using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.EditCollection;
using CollectaMundo.DomainLogic.EditCollection.Models;
using CollectaMundo.Infrastructure.EditCollection;
using Moq;
using System.Data.SQLite;

namespace CollectaMundo.Tests.UnitTests
{
    public class EditCollectionLogicTests
    {
        [Fact]
        public async Task SaveBatchAsync_AddNewCard_WhenNotExisting()
        {
            var repo = new Mock<IEditCollectionRepo>();
            var dummyConn = new SQLiteConnection("Data Source=:memory:");
            dummyConn.Open();

            // This simulates: no existing record matches this card
            repo.Setup(r => r.FindRecordByIdAsync(
                "foo-uuid", "Near Mint", "German", "nonfoil", dummyConn))
                .ReturnsAsync(new List<int>()); // Pure insert

            // When inserting, return card ID 123
            repo.Setup(r => r.AddCardAndReturnIdAsync(
                It.Is<CardSet>(c => c.Uuid == "foo-uuid"),
                dummyConn))
                .ReturnsAsync(123);

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

            var survivor = evt.Survivor!;
            Assert.Equal(123, survivor.CardId);
            Assert.Equal("foo-uuid", survivor.Uuid);
            Assert.Equal("Near Mint", survivor.SelectedCondition);
            Assert.Equal("nonfoil", survivor.SelectedFinish);
            Assert.Equal("German", survivor.Language);
            Assert.Equal(2, survivor.CardsOwned);
            Assert.Equal(1, survivor.CardsForTrade);

            // Verify repo methods called correctly
            repo.Verify(r => r.FindRecordByIdAsync(
                "foo-uuid", "Near Mint", "German", "nonfoil", dummyConn), Times.Once);

            repo.Verify(r => r.AddCardAndReturnIdAsync(
                It.Is<CardSet>(c =>
                    c.Uuid == "foo-uuid" &&
                    c.SelectedCondition == "Near Mint" &&
                    c.SelectedFinish == "nonfoil" &&
                    c.Language == "German" &&
                    c.CardsOwned == 2 &&
                    c.CardsForTrade == 1),
                dummyConn), Times.Once);
        }

        [Fact]
        public async Task SaveBatchAsync_AddNewCard_AddToExisting()
        {
            var repo = new Mock<IEditCollectionRepo>();
            var dummyConn = new SQLiteConnection("Data Source=:memory:");
            dummyConn.Open();

            repo.Setup(r => r.FindRecordByIdAsync("foo-uuid", "Near Mint", "German", "nonfoil", dummyConn)).ReturnsAsync([123]);

            // Existing totals in DB
            repo.Setup(r => r.GetTotalsAsync("foo-uuid", "Near Mint", "German", "nonfoil", dummyConn)).ReturnsAsync((6, 4));

            // New logic updates survivor row
            repo.Setup(r => r.UpdateCardFieldsByIdAsync(
                    123,
                    8, // 6 + 2
                    5, // 4 + 1
                    "Near Mint", "German", "nonfoil",
                    dummyConn))
                .Returns(Task.CompletedTask);

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

            // IMPORTANT: totals should include incoming card
            Assert.Equal(8, evt.Survivor.CardsOwned);    // 6 + 2
            Assert.Equal(5, evt.Survivor.CardsForTrade); // 4 + 1

            repo.Verify(r => r.FindRecordByIdAsync(
                "foo-uuid", "Near Mint", "German", "nonfoil", dummyConn),
                Times.Once);

            repo.Verify(r => r.GetTotalsAsync(
                "foo-uuid", "Near Mint", "German", "nonfoil", dummyConn),
                Times.Once);

            repo.Verify(r => r.UpdateCardFieldsByIdAsync(
                123, 8, 5,
                "Near Mint", "German", "nonfoil",
                dummyConn),
                Times.Once);
        }

        [Fact]
        public async Task SaveBatchAsync_EditCard_DeleteByZero()
        {
            var repo = new Mock<IEditCollectionRepo>();
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
            var repo = new Mock<IEditCollectionRepo>();

            // IMPORTANT: connection must be OPEN, because logic begins a transaction
            var dummyConn = new SQLiteConnection("Data Source=:memory:");
            dummyConn.Open();

            // Arrange: Return single existing match (no merge)
            repo.Setup(r => r.FindRecordByIdAsync(
                    "foo-uuid", "Near Mint", "German", "nonfoil", dummyConn))
                .ReturnsAsync(new List<int> { 123 });

            // Arrange: Simulate totals already in DB (0,0 means only this card contributes)
            repo.Setup(r => r.GetTotalsAsync(
                    "foo-uuid", "Near Mint", "German", "nonfoil", dummyConn))
                .ReturnsAsync((0, 0));

            // Arrange: Expect survivor row update
            repo.Setup(r => r.UpdateCardFieldsByIdAsync(
                    123, 3, 1,
                    "Near Mint", "German", "nonfoil",
                    dummyConn))
                .Returns(Task.CompletedTask);

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

            // Assert: business logic
            var evt = Assert.Single(results);
            Assert.Equal(CardChangeEventArgs.ChangeType.Upsert, evt.Type);
            Assert.NotNull(evt.Survivor);

            var survivor = evt.Survivor!;
            Assert.Equal(123, survivor.CardId);
            Assert.Equal("foo-uuid", survivor.Uuid);
            Assert.Equal("Near Mint", survivor.SelectedCondition);
            Assert.Equal("nonfoil", survivor.SelectedFinish);
            Assert.Equal("German", survivor.Language);
            Assert.Equal(3, survivor.CardsOwned);
            Assert.Equal(1, survivor.CardsForTrade);

            // Verify: correct repo interaction (no merge = no delete call)
            repo.Verify(r => r.FindRecordByIdAsync(
                "foo-uuid", "Near Mint", "German", "nonfoil", dummyConn), Times.Once);

            repo.Verify(r => r.GetTotalsAsync(
                "foo-uuid", "Near Mint", "German", "nonfoil", dummyConn), Times.Once);

            repo.Verify(r => r.UpdateCardFieldsByIdAsync(
                123, 3, 1,
                "Near Mint", "German", "nonfoil",
                dummyConn), Times.Once);

            repo.Verify(r => r.DeleteCardsByIdsAsync(
                It.IsAny<IEnumerable<int>>(),
                It.IsAny<SQLiteConnection>()), Times.Never);
        }

        [Fact]
        public async Task SaveBatchAsync_EditCard_Update_merge()
        {
            var repo = new Mock<IEditCollectionRepo>();

            // IMPORTANT: must be OPEN because logic begins a transaction
            var dummyConn = new SQLiteConnection("Data Source=:memory:");
            dummyConn.Open();

            // Simulate finding multiple matches for the business key
            repo.Setup(r => r.FindRecordByIdAsync(
                    "foo-uuid", "Near Mint", "German", "nonfoil", dummyConn))
                .ReturnsAsync(new List<int> { 123, 456, 789 });

            // Return existing totals before applying this edit
            repo.Setup(r => r.GetTotalsAsync(
                    "foo-uuid", "Near Mint", "German", "nonfoil", dummyConn))
                .ReturnsAsync((6, 4));

            // Expect update of survivor row (keepId=123) with combined totals
            repo.Setup(r => r.UpdateCardFieldsByIdAsync(
                    123,
                    9,  // 6 + 3
                    5,  // 4 + 1
                    "Near Mint", "German", "nonfoil",
                    dummyConn))
                .Returns(Task.CompletedTask);

            // Expect deletion of duplicates
            repo.Setup(r => r.DeleteCardsByIdsAsync(
                    It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 456, 789 })),
                    dummyConn))
                .Returns(Task.CompletedTask);

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

            // Assert - business logic
            var evt = Assert.Single(results);
            Assert.Equal(CardChangeEventArgs.ChangeType.Upsert, evt.Type);
            Assert.NotNull(evt.Survivor);

            var survivor = evt.Survivor!;
            Assert.Equal(123, survivor.CardId);
            Assert.Equal("foo-uuid", survivor.Uuid);
            Assert.Equal("Near Mint", survivor.SelectedCondition);
            Assert.Equal("nonfoil", survivor.SelectedFinish);
            Assert.Equal("German", survivor.Language);
            Assert.Equal(9, survivor.CardsOwned);     // 6 + 3
            Assert.Equal(5, survivor.CardsForTrade);  // 4 + 1

            // Removed IDs should be the non-survivors
            Assert.True(evt.Removed.SequenceEqual(new[] { 456, 789 }));

            // Verify repo interactions
            repo.Verify(r => r.FindRecordByIdAsync(
                "foo-uuid", "Near Mint", "German", "nonfoil", dummyConn), Times.Once);

            repo.Verify(r => r.GetTotalsAsync(
                "foo-uuid", "Near Mint", "German", "nonfoil", dummyConn), Times.Once);

            repo.Verify(r => r.DeleteCardsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 456, 789 })),
                dummyConn), Times.Once);

            repo.Verify(r => r.UpdateCardFieldsByIdAsync(
                123, 9, 5, "Near Mint", "German", "nonfoil", dummyConn), Times.Once);
        }
    }
}
