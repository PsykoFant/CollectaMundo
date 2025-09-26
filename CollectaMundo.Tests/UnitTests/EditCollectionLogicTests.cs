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
            var repo = new Mock<IEditCollectionRepo>();
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
            var repo = new Mock<IEditCollectionRepo>();
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
}
