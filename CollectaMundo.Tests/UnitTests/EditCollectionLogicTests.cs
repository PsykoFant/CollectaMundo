using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.EditCollection;
using CollectaMundo.DomainLogic.Shared;

namespace CollectaMundo.Tests.UnitTests
{
    public class EditCollectionLogicTests
    {
        // Snapshot stub for unit tests: represents an empty in-memory collection.
        private sealed class EmptySnapshot : ICollectionSnapshot
        {
            public bool TryGetById(int cardId, out MyCollectionRow row)
            {
                row = default!;
                return false;
            }

            public bool TryGetByIdentity(CollectionIdentity identity, out MyCollectionRow row)
            {
                row = default!;
                return false;
            }
        }
        private sealed class TestSnapshot(IEnumerable<MyCollectionRow> rows) : ICollectionSnapshot
        {
            private readonly Dictionary<int, MyCollectionRow> _byId = rows.ToDictionary(r => r.CardId);
            private readonly Dictionary<CollectionIdentity, MyCollectionRow> _byIdentity = rows.ToDictionary(r => r.Identity);

            public bool TryGetById(int cardId, out MyCollectionRow row) => _byId.TryGetValue(cardId, out row!);

            public bool TryGetByIdentity(CollectionIdentity identity, out MyCollectionRow row) => _byIdentity.TryGetValue(identity, out row!);
        }

        [Fact]
        public void PlanBatch_AddNewCard_WhenNotExisting_SchedulesInsert()
        {
            // Arrange: snapshot contains nothing (no existing identity)
            var snapshot = new EmptySnapshot();

            var logic = new EditCollectionLogic();
            // ^ assuming your refactored logic no longer needs repo in ctor.
            // If it still has dependencies, inject them here (but repo should be gone).

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
            var plan = logic.PlanBatch([newCard], snapshot, isEdit: false);

            // Assert: Insert scheduled
            Assert.Empty(plan.DeleteIds);
            Assert.Empty(plan.Updates);

            var insert = Assert.Single(plan.Inserts);
            Assert.Equal("foo-uuid", insert.Identity.Uuid);
            Assert.Equal("Near Mint", insert.Identity.Condition);
            Assert.Equal("German", insert.Identity.Language);
            Assert.Equal("nonfoil", insert.Identity.Finish);
            Assert.Equal(2, insert.CardsOwned);
            Assert.Equal(1, insert.CardsForTrade);

            // Assert: ChangeSet represents the in-memory upsert (CardId is still null at plan time)
            Assert.Empty(plan.ChangeSet.RemovedIds);

            var survivor = Assert.Single(plan.ChangeSet.AddedOrUpdated);
            Assert.Same(newCard, survivor); // important: plan uses the same object for apply
            Assert.Null(survivor.CardId);

            Assert.Equal("foo-uuid", survivor.Uuid);
            Assert.Equal("Near Mint", survivor.SelectedCondition);
            Assert.Equal("nonfoil", survivor.SelectedFinish);
            Assert.Equal("German", survivor.Language);
            Assert.Equal(2, survivor.CardsOwned);
            Assert.Equal(1, survivor.CardsForTrade);
        }

        [Fact]
        public void PlanBatch_AddNewCard_AddsToExisting()
        {
            // Arrange: snapshot contains an existing matching identity
            var existingIdentity = new CollectionIdentity(
                "foo-uuid",
                "Near Mint",
                "German",
                "nonfoil");

            var snapshot = new TestSnapshot(
                rows:
                [
            new MyCollectionRow
            {
                CardId = 123,
                Identity = existingIdentity,
                CardsOwned = 6,
                CardsForTrade = 4
            }
                ]);

            var logic = new EditCollectionLogic();

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
            var plan = logic.PlanBatch(
                [newCard],
                snapshot,
                isEdit: false);

            // Assert: no deletes, no inserts
            Assert.Empty(plan.DeleteIds);
            Assert.Empty(plan.Inserts);

            // Exactly one update
            var update = Assert.Single(plan.Updates);
            Assert.Equal(123, update.CardId);
            Assert.Equal(existingIdentity, update.Identity);

            // Totals are merged
            Assert.Equal(8, update.CardsOwned);    // 6 + 2
            Assert.Equal(5, update.CardsForTrade); // 4 + 1

            // ChangeSet reflects survivor
            Assert.Empty(plan.ChangeSet.RemovedIds);

            var survivor = Assert.Single(plan.ChangeSet.AddedOrUpdated);
            Assert.Same(newCard, survivor);

            Assert.Equal(123, survivor.CardId);
            Assert.Equal("foo-uuid", survivor.Uuid);
            Assert.Equal("Near Mint", survivor.SelectedCondition);
            Assert.Equal("nonfoil", survivor.SelectedFinish);
            Assert.Equal("German", survivor.Language);
            Assert.Equal(8, survivor.CardsOwned);
            Assert.Equal(5, survivor.CardsForTrade);
        }

        [Fact]
        public void PlanBatch_EditCard_DeleteByZero()
        {
            // Arrange: snapshot contains the existing card
            var existingIdentity = new CollectionIdentity(
                "foo-uuid",
                "Near Mint",
                "German",
                "nonfoil");

            var snapshot = new TestSnapshot(
                rows:
                [
            new MyCollectionRow
            {
                CardId = 123,
                Identity = existingIdentity,
                CardsOwned = 5,
                CardsForTrade = 1
            }
                ]);

            var logic = new EditCollectionLogic();

            var card = new CardSet
            {
                CardId = 123,
                // NOTE: identity technically does not matter for delete-by-zero,
                // but we include it here to match a realistic edit scenario.
                Uuid = "foo-uuid",
                SelectedCondition = "Near Mint",
                SelectedFinish = "nonfoil",
                Language = "German",
                CardsOwned = 0,   // <-- deletion trigger
                CardsForTrade = 1
            };

            // Act
            var plan = logic.PlanBatch(
                [card],
                snapshot,
                isEdit: true);

            // Assert: delete scheduled
            var deletedId = Assert.Single(plan.DeleteIds);
            Assert.Equal(123, deletedId);

            // No updates or inserts
            Assert.Empty(plan.Updates);
            Assert.Empty(plan.Inserts);

            // ChangeSet reflects deletion
            Assert.Empty(plan.ChangeSet.AddedOrUpdated);

            var removed = Assert.Single(plan.ChangeSet.RemovedIds);
            Assert.Equal(123, removed);
        }

        [Fact]
        public void PlanBatch_EditCard_Update_NoMerge()
        {
            // Arrange: snapshot contains the original row with the same identity
            var identity = new CollectionIdentity(
                "foo-uuid",
                "Near Mint",
                "German",
                "nonfoil");

            var snapshot = new TestSnapshot(rows:
                [new MyCollectionRow
                {
                    CardId = 123,
                    Identity = identity,
                    CardsOwned = 2,
                    CardsForTrade = 0
                }
                ]);

            var logic = new EditCollectionLogic();

            var card = new CardSet
            {
                CardId = 123,
                Uuid = "foo-uuid",
                SelectedCondition = "Near Mint",
                SelectedFinish = "nonfoil",
                Language = "German",
                CardsOwned = 3,      // absolute values
                CardsForTrade = 1
            };

            // Act
            var plan = logic.PlanBatch([card], snapshot, isEdit: true);

            // Assert: no deletes, no inserts
            Assert.Empty(plan.DeleteIds);
            Assert.Empty(plan.Inserts);

            // Exactly one update
            var update = Assert.Single(plan.Updates);

            Assert.Equal(123, update.CardId);
            Assert.Equal(identity, update.Identity);
            Assert.Equal(3, update.CardsOwned);
            Assert.Equal(1, update.CardsForTrade);

            // ChangeSet: one upsert, no removals
            Assert.Empty(plan.ChangeSet.RemovedIds);

            var survivor = Assert.Single(plan.ChangeSet.AddedOrUpdated);
            Assert.Same(card, survivor);

            Assert.Equal(123, survivor.CardId);
            Assert.Equal("foo-uuid", survivor.Uuid);
            Assert.Equal("Near Mint", survivor.SelectedCondition);
            Assert.Equal("nonfoil", survivor.SelectedFinish);
            Assert.Equal("German", survivor.Language);
            Assert.Equal(3, survivor.CardsOwned);
            Assert.Equal(1, survivor.CardsForTrade);
        }

        [Fact]
        public void PlanBatch_EditCard_Update_Merge()
        {
            // Arrange
            var survivorIdentity = new CollectionIdentity(
                "foo-uuid",
                "Near Mint",
                "German",
                "nonfoil");

            var snapshot = new TestSnapshot(
                rows:
                [
            // Survivor row
            new MyCollectionRow
            {
                CardId = 456,
                Identity = survivorIdentity,
                CardsOwned = 6,
                CardsForTrade = 4
            },

            // Current row being edited
            new MyCollectionRow
            {
                CardId = 123,
                Identity = new CollectionIdentity(
                    "foo-uuid",
                    "Excellent",   // original condition
                    "German",
                    "nonfoil"),
                CardsOwned = 3,
                CardsForTrade = 1
            }
                ]);

            var logic = new EditCollectionLogic();

            var editedCard = new CardSet
            {
                CardId = 123,
                Uuid = "foo-uuid",

                // Identity changed to collide with survivor
                SelectedCondition = "Near Mint",
                SelectedFinish = "nonfoil",
                Language = "German",

                CardsOwned = 3,
                CardsForTrade = 1
            };

            // Act
            var plan = logic.PlanBatch([editedCard], snapshot, isEdit: true);

            // Assert: DELETE current row
            var deletedId = Assert.Single(plan.DeleteIds);
            Assert.Equal(123, deletedId);

            // Assert: exactly one UPDATE (the survivor)
            var update = Assert.Single(plan.Updates);

            Assert.Equal(456, update.CardId);
            Assert.Equal(survivorIdentity, update.Identity);

            // Merged totals
            Assert.Equal(9, update.CardsOwned);     // 6 + 3
            Assert.Equal(5, update.CardsForTrade);  // 4 + 1

            // No inserts
            Assert.Empty(plan.Inserts);

            // ChangeSet
            Assert.Equal([123], plan.ChangeSet.RemovedIds);

            var survivor = Assert.Single(plan.ChangeSet.AddedOrUpdated);
            Assert.Same(editedCard, survivor);

            Assert.Equal(456, survivor.CardId);
            Assert.Equal("foo-uuid", survivor.Uuid);
            Assert.Equal("Near Mint", survivor.SelectedCondition);
            Assert.Equal("German", survivor.Language);
            Assert.Equal("nonfoil", survivor.SelectedFinish);
            Assert.Equal(9, survivor.CardsOwned);
            Assert.Equal(5, survivor.CardsForTrade);
        }

        [Fact]
        public void PlanBatch_EditCard_Merge_UsesOnlySurvivorTotals()
        {
            // Arrange
            var identity = new CollectionIdentity(
                "foo-uuid",
                "Near Mint",
                "German",
                "nonfoil");

            var snapshot = new TestSnapshot(
                rows:
                [
            // Survivor row (already in collection)
            new MyCollectionRow
            {
                CardId = 456,
                Identity = identity,
                CardsOwned = 5,
                CardsForTrade = 2
            },

            // Current row being edited
            new MyCollectionRow
            {
                CardId = 123,
                Identity = new CollectionIdentity(
                    "foo-uuid",
                    "Excellent",   // original condition
                    "German",
                    "nonfoil"),
                CardsOwned = 2,
                CardsForTrade = 1
            }
                ]);

            var logic = new EditCollectionLogic();

            var editedCard = new CardSet
            {
                CardId = 123,
                Uuid = "foo-uuid",

                // Identity changed → collision
                SelectedCondition = "Near Mint",
                SelectedFinish = "nonfoil",
                Language = "German",

                CardsOwned = 2,
                CardsForTrade = 1
            };

            // Act
            var plan = logic.PlanBatch([editedCard], snapshot, isEdit: true);

            // Assert: current row deleted
            var deletedId = Assert.Single(plan.DeleteIds);
            Assert.Equal(123, deletedId);

            // Assert: survivor updated
            var update = Assert.Single(plan.Updates);
            Assert.Equal(456, update.CardId);

            // Totals = survivor + editedCard ONLY
            Assert.Equal(7, update.CardsOwned);     // 5 + 2
            Assert.Equal(3, update.CardsForTrade);  // 2 + 1

            // No inserts
            Assert.Empty(plan.Inserts);

            // ChangeSet
            Assert.Equal([123], plan.ChangeSet.RemovedIds);

            var survivor = Assert.Single(plan.ChangeSet.AddedOrUpdated);
            Assert.Equal(456, survivor.CardId);
            Assert.Equal(7, survivor.CardsOwned);
            Assert.Equal(3, survivor.CardsForTrade);
        }
    }
}
