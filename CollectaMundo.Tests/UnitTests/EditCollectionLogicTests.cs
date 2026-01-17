using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.EditCollection;
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
                .ReturnsAsync([]); // Pure insert

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
            var changeSets = await logic.SaveBatchAsync([newCard], isEdit: false, dummyConn);

            // Assert: one batch -> one change set
            var changeSet = Assert.Single(changeSets);

            // No removals on pure insert
            Assert.Empty(changeSet.RemovedIds);

            // Exactly one upsert
            var survivor = Assert.Single(changeSet.AddedOrUpdated);

            Assert.Equal(123, survivor.CardId);
            Assert.Equal("foo-uuid", survivor.Uuid);
            Assert.Equal("Near Mint", survivor.SelectedCondition);
            Assert.Equal("nonfoil", survivor.SelectedFinish);
            Assert.Equal("German", survivor.Language);
            Assert.Equal(2, survivor.CardsOwned);
            Assert.Equal(1, survivor.CardsForTrade);

            // Verify repo calls
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

            // Existing matching record
            repo.Setup(r => r.FindRecordByIdAsync(
                    "foo-uuid", "Near Mint", "German", "nonfoil", dummyConn))
                .ReturnsAsync(new List<int> { 123 });

            // Existing totals in DB
            repo.Setup(r => r.GetTotalsAsync(
                    "foo-uuid", "Near Mint", "German", "nonfoil", dummyConn))
                .ReturnsAsync((6, 4));

            // Survivor row update
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
            var changeSets = await logic.SaveBatchAsync(
                new[] { newCard },
                isEdit: false,
                dummyConn);

            // Assert: single batch → single change set
            var changeSet = Assert.Single(changeSets);

            // No rows removed in add-to-existing scenario
            Assert.Empty(changeSet.RemovedIds);

            // Exactly one upsert (the survivor)
            var survivor = Assert.Single(changeSet.AddedOrUpdated);

            Assert.Equal(123, survivor.CardId);
            Assert.Equal("foo-uuid", survivor.Uuid);
            Assert.Equal("Near Mint", survivor.SelectedCondition);
            Assert.Equal("nonfoil", survivor.SelectedFinish);
            Assert.Equal("German", survivor.Language);

            // IMPORTANT: totals include incoming card
            Assert.Equal(8, survivor.CardsOwned);    // 6 + 2
            Assert.Equal(5, survivor.CardsForTrade); // 4 + 1

            // Verify repo interactions
            repo.Verify(r => r.FindRecordByIdAsync(
                "foo-uuid", "Near Mint", "German", "nonfoil", dummyConn),
                Times.Once);

            repo.Verify(r => r.GetTotalsAsync(
                "foo-uuid", "Near Mint", "German", "nonfoil", dummyConn),
                Times.Once);

            repo.Verify(r => r.UpdateCardFieldsByIdAsync(
                123,
                8,
                5,
                "Near Mint", "German", "nonfoil",
                dummyConn),
                Times.Once);
        }

        [Fact]
        public async Task SaveBatchAsync_EditCard_DeleteByZero()
        {
            var repo = new Mock<IEditCollectionRepo>();
            var dummyConn = new SQLiteConnection("Data Source=:memory:");
            dummyConn.Open();

            // Arrange: deletion path
            repo.Setup(r => r.DeleteCardByIdAsync(
                    It.IsAny<CardSet>(),
                    dummyConn))
                .Returns(Task.CompletedTask);

            var logic = new EditCollectionLogic(repo.Object);

            var card = new CardSet
            {
                CardId = 123,
                Uuid = "foo-uuid",
                SelectedCondition = "Near Mint",
                SelectedFinish = "nonfoil",
                Language = "German",
                CardsOwned = 0,   // ← deletion trigger
                CardsForTrade = 1
            };

            // Act
            var changeSets = await logic.SaveBatchAsync(
                new[] { card },
                isEdit: true,
                dummyConn);

            // Assert: exactly one change set
            var changeSet = Assert.Single(changeSets);

            // Delete-by-zero → no upserts
            Assert.Empty(changeSet.AddedOrUpdated);

            // Exactly one removed ID
            var removedId = Assert.Single(changeSet.RemovedIds);
            Assert.Equal(123, removedId);

            // Verify repo interaction
            repo.Verify(r => r.DeleteCardByIdAsync(card, dummyConn), Times.Once);
        }

        [Fact]
        public async Task SaveBatchAsync_EditCard_Update_no_merge()
        {
            var repo = new Mock<IEditCollectionRepo>();

            var dummyConn = new SQLiteConnection("Data Source=:memory:");
            dummyConn.Open();

            // Arrange: Return single existing match (no merge)
            repo.Setup(r => r.FindRecordByIdAsync(
                    "foo-uuid", "Near Mint", "German", "nonfoil", dummyConn))
                .ReturnsAsync(new List<int> { 123 });

            // Arrange: Expect survivor row update to set absolute values
            repo.Setup(r => r.UpdateCardFieldsByIdAsync(
                    123, 3, 1,
                    "Near Mint", "German", "nonfoil",
                    dummyConn))
                .Returns(Task.CompletedTask);

            var logic = new EditCollectionLogic(repo.Object);

            var card = new CardSet
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
            var changeSets = await logic.SaveBatchAsync(
                [card],
                isEdit: true,
                dummyConn);

            // Assert: exactly one change set
            var changeSet = Assert.Single(changeSets);

            // No merge → no removals
            Assert.Empty(changeSet.RemovedIds);

            // Exactly one upsert
            var survivor = Assert.Single(changeSet.AddedOrUpdated);

            Assert.Equal(123, survivor.CardId);
            Assert.Equal("foo-uuid", survivor.Uuid);
            Assert.Equal("Near Mint", survivor.SelectedCondition);
            Assert.Equal("nonfoil", survivor.SelectedFinish);
            Assert.Equal("German", survivor.Language);
            Assert.Equal(3, survivor.CardsOwned);
            Assert.Equal(1, survivor.CardsForTrade);

            // Verify: correct repo interaction
            repo.Verify(r => r.FindRecordByIdAsync(
                "foo-uuid", "Near Mint", "German", "nonfoil", dummyConn),
                Times.Once);

            repo.Verify(r => r.UpdateCardFieldsByIdAsync(
                123, 3, 1,
                "Near Mint", "German", "nonfoil",
                dummyConn),
                Times.Once);

            // ✅ Simple edit → totals NOT used
            repo.Verify(r => r.GetTotalsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<SQLiteConnection>()), Times.Never);

            repo.Verify(r => r.GetTotalsExcludingIdAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<SQLiteConnection>()), Times.Never);

            // ✅ No merge → no deletions
            repo.Verify(r => r.DeleteCardsByIdsAsync(
                It.IsAny<IEnumerable<int>>(),
                It.IsAny<SQLiteConnection>()), Times.Never);
        }

        [Fact]
        public async Task SaveBatchAsync_EditCard_Update_merge()
        {
            var repo = new Mock<IEditCollectionRepo>();
            var dummyConn = new SQLiteConnection("Data Source=:memory:");
            dummyConn.Open();

            // Simulate finding multiple matches for the business key
            repo.Setup(r => r.FindRecordByIdAsync(
                    "foo-uuid", "Near Mint", "German", "nonfoil", dummyConn))
                .ReturnsAsync(new List<int> { 123, 456, 789 });

            // EDIT + MERGE → exclude current row
            repo.Setup(r => r.GetTotalsExcludingIdAsync(
                    "foo-uuid", "Near Mint", "German", "nonfoil", 123, dummyConn))
                .ReturnsAsync((6, 4)); // from rows 456 + 789

            // Expect update of survivor row
            repo.Setup(r => r.UpdateCardFieldsByIdAsync(
                    123,
                    9, 5, // 6 + 3, 4 + 1
                    "Near Mint", "German", "nonfoil",
                    dummyConn))
                .Returns(Task.CompletedTask);

            // Expect deletion of duplicates
            repo.Setup(r => r.DeleteCardsByIdsAsync(
                    It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 456, 789 })),
                    dummyConn))
                .Returns(Task.CompletedTask);

            var logic = new EditCollectionLogic(repo.Object);

            var card = new CardSet
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
            var changeSets = await logic.SaveBatchAsync([card], isEdit: true, dummyConn);

            // Assert: exactly one change set
            var changeSet = Assert.Single(changeSets);

            // Removed IDs should be the non-survivors
            Assert.Equal(new[] { 456, 789 }, changeSet.RemovedIds.OrderBy(x => x));

            // Exactly one upsert (the survivor)
            var survivor = Assert.Single(changeSet.AddedOrUpdated);

            Assert.Equal(123, survivor.CardId);
            Assert.Equal("foo-uuid", survivor.Uuid);
            Assert.Equal("Near Mint", survivor.SelectedCondition);
            Assert.Equal("nonfoil", survivor.SelectedFinish);
            Assert.Equal("German", survivor.Language);
            Assert.Equal(9, survivor.CardsOwned);     // 6 + 3
            Assert.Equal(5, survivor.CardsForTrade);  // 4 + 1

            // Verify correct calls
            repo.Verify(r => r.FindRecordByIdAsync(
                "foo-uuid", "Near Mint", "German", "nonfoil", dummyConn),
                Times.Once);

            repo.Verify(r => r.GetTotalsExcludingIdAsync(
                "foo-uuid", "Near Mint", "German", "nonfoil", 123, dummyConn),
                Times.Once);

            repo.Verify(r => r.UpdateCardFieldsByIdAsync(
                123, 9, 5,
                "Near Mint", "German", "nonfoil",
                dummyConn),
                Times.Once);

            repo.Verify(r => r.DeleteCardsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 456, 789 })),
                dummyConn),
                Times.Once);

            // ✅ Confirm GetTotalsAsync is NOT called
            repo.Verify(r => r.GetTotalsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<SQLiteConnection>()),
                Times.Never);
        }
        [Fact]
        public async Task SaveBatchAsync_EditCard_Merge_ExcludesSelfFromTotals()
        {
            var repo = new Mock<IEditCollectionRepo>();
            var dummyConn = new SQLiteConnection("Data Source=:memory:");
            dummyConn.Open();

            // Setup: Find multiple matches, causing a merge
            repo.Setup(r => r.FindRecordByIdAsync(
                    "foo-uuid", "Near Mint", "German", "nonfoil", dummyConn))
                .ReturnsAsync([123, 456]);

            // Setup: Return only the *other* row's totals when excluding self
            repo.Setup(r => r.GetTotalsExcludingIdAsync(
                    "foo-uuid", "Near Mint", "German", "nonfoil", 123, dummyConn))
                .ReturnsAsync((5, 2));  // Row 456's values

            // Expect merged totals:
            // Owned: 5 (other) + 2 (current) = 7
            // Trade: 2 (other) + 1 (current) = 3
            repo.Setup(r => r.UpdateCardFieldsByIdAsync(
                    123,
                    7, 3,
                    "Near Mint", "German", "nonfoil",
                    dummyConn))
                .Returns(Task.CompletedTask);

            // Setup: Delete duplicate row
            repo.Setup(r => r.DeleteCardsByIdsAsync(
                    It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 456 })),
                    dummyConn))
                .Returns(Task.CompletedTask);

            var logic = new EditCollectionLogic(repo.Object);

            var editedCard = new CardSet
            {
                CardId = 123,
                Uuid = "foo-uuid",
                SelectedCondition = "Near Mint",
                SelectedFinish = "nonfoil",
                Language = "German",
                CardsOwned = 2,
                CardsForTrade = 1
            };

            // Act
            var changeSets = await logic.SaveBatchAsync(
                new[] { editedCard },
                isEdit: true,
                dummyConn);

            // Assert: exactly one change set
            var changeSet = Assert.Single(changeSets);

            // Removed IDs should include only the duplicate
            Assert.Equal(new[] { 456 }, changeSet.RemovedIds);

            // Exactly one upsert (the survivor)
            var survivor = Assert.Single(changeSet.AddedOrUpdated);

            Assert.Equal(123, survivor.CardId);
            Assert.Equal("foo-uuid", survivor.Uuid);
            Assert.Equal("Near Mint", survivor.SelectedCondition);
            Assert.Equal("nonfoil", survivor.SelectedFinish);
            Assert.Equal("German", survivor.Language);
            Assert.Equal(7, survivor.CardsOwned);     // 5 + 2
            Assert.Equal(3, survivor.CardsForTrade);  // 2 + 1

            // Verify exclusion logic
            repo.Verify(r => r.GetTotalsExcludingIdAsync(
                "foo-uuid", "Near Mint", "German", "nonfoil", 123, dummyConn),
                Times.Once);

            // Verify delete and update
            repo.Verify(r => r.DeleteCardsByIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 456 })),
                dummyConn),
                Times.Once);

            repo.Verify(r => r.UpdateCardFieldsByIdAsync(
                123, 7, 3,
                "Near Mint", "German", "nonfoil",
                dummyConn),
                Times.Once);

            // ✅ Ensure GetTotalsAsync is NOT used
            repo.Verify(r => r.GetTotalsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<SQLiteConnection>()),
                Times.Never);
        }


    }
}
