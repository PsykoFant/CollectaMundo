using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.CollectionMutations;
using CollectaMundo.DomainLogic.Shared;
using CollectaMundo.DomainLogic.Shared.Models;

namespace CollectaMundo.Tests.UnitTests
{
    public class CollectionMutationsLogicTests
    {
        // Snapshot stub for unit tests: represents an empty in-memory collection.
        private sealed class EmptySnapshot : ICollectionSnapshot
        {
            public IReadOnlyCollection<MyCollectionRow> Rows { get; } = [];
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
        private sealed class TestSnapshot : ICollectionSnapshot
        {
            private readonly Dictionary<int, MyCollectionRow> _byId;
            private readonly Dictionary<CollectionIdentity, MyCollectionRow> _byIdentity;
            public IReadOnlyCollection<MyCollectionRow> Rows { get; }
            public TestSnapshot(IEnumerable<MyCollectionRow> rows)
            {
                var rowList = rows.ToList();
                Rows = rowList;
                _byId = rowList.ToDictionary(r => r.CardId);
                _byIdentity = rowList.ToDictionary(r => r.Identity);
            }
            public bool TryGetById(int cardId, out MyCollectionRow row) => _byId.TryGetValue(cardId, out row!);
            public bool TryGetByIdentity(CollectionIdentity identity, out MyCollectionRow row) => _byIdentity.TryGetValue(identity, out row!);
        }

        [Fact]
        public void PlanIdentityRewriteBatch_NewRow_WhenIdentityMissing_SchedulesInsert()
        {
            // Arrange: snapshot contains nothing (no existing identity)
            var snapshot = new EmptySnapshot();

            var logic = new CollectionMutationsLogic();

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
            var plan = logic.PlanIdentityRewriteBatch([newCard], snapshot);

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
        public void PlanIdentityRewriteBatch_AddNewCard_AddsToExisting()
        {
            // Arrange: snapshot contains an existing matching identity
            var existingIdentity = new CollectionIdentity(
                "foo-uuid",
                "Near Mint",
                "German",
                "nonfoil",
                null,
                null);

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

            var logic = new CollectionMutationsLogic();

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
            var plan = logic.PlanIdentityRewriteBatch(
                [newCard],
                snapshot);

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
        public void PlanIdentityRewriteBatch_EditCard_DeleteByZero()
        {
            // Arrange: snapshot contains the existing card
            var existingIdentity = new CollectionIdentity(
                "foo-uuid",
                "Near Mint",
                "German",
                "nonfoil",
                null,
                null);

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

            var logic = new CollectionMutationsLogic();

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
            var plan = logic.PlanIdentityRewriteBatch(
                [card],
                snapshot);

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
        public void PlanIdentityRewriteBatch_EditCard_Update_NoMerge()
        {
            // Arrange: snapshot contains the original row with the same identity
            var identity = new CollectionIdentity(
                "foo-uuid",
                "Near Mint",
                "German",
                "nonfoil",
                null,
                null);

            var snapshot = new TestSnapshot(rows:
                [new MyCollectionRow
                {
                    CardId = 123,
                    Identity = identity,
                    CardsOwned = 2,
                    CardsForTrade = 0
                }
                ]);

            var logic = new CollectionMutationsLogic();

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
            var plan = logic.PlanIdentityRewriteBatch([card], snapshot);

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
        public void PlanIdentityRewriteBatch_EditCard_Update_Merge()
        {
            // Arrange
            var survivorIdentity = new CollectionIdentity(
                "foo-uuid",
                "Near Mint",
                "German",
                "nonfoil",
                null,
                null);

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
                    "nonfoil",
                    null,
                    null),
                CardsOwned = 3,
                CardsForTrade = 1
            }
                ]);

            var logic = new CollectionMutationsLogic();

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
            var plan = logic.PlanIdentityRewriteBatch([editedCard], snapshot);

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
        public void PlanIdentityRewriteBatch_EditCard_Merge_UsesOnlySurvivorTotals()
        {
            // Arrange
            var identity = new CollectionIdentity(
                "foo-uuid",
                "Near Mint",
                "German",
                "nonfoil",
                null,
                null);

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
                    "nonfoil",
                    null,
                    null),
                CardsOwned = 2,
                CardsForTrade = 1
            }
                ]);

            var logic = new CollectionMutationsLogic();

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
            var plan = logic.PlanIdentityRewriteBatch([editedCard], snapshot);

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

        [Fact]
        public void PlanIdentityRewriteBatch_EditTwoCards_ToSameTargetIdentity_MergesIntoSingleSurvivor()
        {
            // Arrange
            var targetIdentity = new CollectionIdentity(
                "foo-uuid",
                "Near Mint",
                "German",
                "nonfoil",
                null,
                null);

            var snapshot = new TestSnapshot(rows:
                [new MyCollectionRow
                {
                    CardId = 101,
                    Identity = new CollectionIdentity(
                        "foo-uuid",
                        "Excellent",
                        "German",
                        "nonfoil",
                        null,
                        null),
                    CardsOwned = 2,
                    CardsForTrade = 1
                },
                new MyCollectionRow
                {
                    CardId = 202,
                    Identity = new CollectionIdentity(
                        "foo-uuid",
                        "Good",
                        "German",
                        "nonfoil",
                        null,
                        null),
                    CardsOwned = 3,
                    CardsForTrade = 1
                }
                ]);

            var logic = new CollectionMutationsLogic();

            var editedA = new CardSet
            {
                CardId = 101,
                Uuid = "foo-uuid",
                SelectedCondition = "Near Mint",
                SelectedFinish = "nonfoil",
                Language = "German",
                CardsOwned = 2,
                CardsForTrade = 1
            };

            var editedB = new CardSet
            {
                CardId = 202,
                Uuid = "foo-uuid",
                SelectedCondition = "Near Mint",
                SelectedFinish = "nonfoil",
                Language = "German",
                CardsOwned = 3,
                CardsForTrade = 1
            };

            // Act
            var plan = logic.PlanIdentityRewriteBatch([editedA, editedB], snapshot);

            // Assert: one row survives, the other is deleted
            var deletedId = Assert.Single(plan.DeleteIds);
            Assert.Equal(202, deletedId);

            var update = Assert.Single(plan.Updates);
            Assert.Equal(101, update.CardId);
            Assert.Equal(targetIdentity, update.Identity);
            Assert.Equal(5, update.CardsOwned);      // 2 + 3
            Assert.Equal(2, update.CardsForTrade);   // 1 + 1

            Assert.Empty(plan.Inserts);

            Assert.Equal([202], plan.ChangeSet.RemovedIds);

            var survivor = Assert.Single(plan.ChangeSet.AddedOrUpdated);
            Assert.Same(editedB, survivor);

            Assert.Equal(101, survivor.CardId);
            Assert.Equal("foo-uuid", survivor.Uuid);
            Assert.Equal("Near Mint", survivor.SelectedCondition);
            Assert.Equal("German", survivor.Language);
            Assert.Equal("nonfoil", survivor.SelectedFinish);
            Assert.Equal(5, survivor.CardsOwned);
            Assert.Equal(2, survivor.CardsForTrade);
        }

        [Fact]
        public void PlanIdentityRewriteBatch_TwoNewRowsSameIdentity_CollapsesToSingleInsert()
        {
            var snapshot = new EmptySnapshot();
            var logic = new CollectionMutationsLogic();

            var cardA = new CardSet
            {
                Uuid = "foo-uuid",
                SelectedCondition = "Near Mint",
                SelectedFinish = "nonfoil",
                Language = "English",
                CardsOwned = 1,
                CardsForTrade = 0
            };

            var cardB = new CardSet
            {
                Uuid = "foo-uuid",
                SelectedCondition = "Near Mint",
                SelectedFinish = "nonfoil",
                Language = "English",
                CardsOwned = 2,
                CardsForTrade = 1
            };

            var plan = logic.PlanIdentityRewriteBatch([cardA, cardB], snapshot);

            Assert.Empty(plan.DeleteIds);
            Assert.Empty(plan.Updates);

            var insert = Assert.Single(plan.Inserts);
            Assert.Equal("foo-uuid", insert.Identity.Uuid);
            Assert.Equal("Near Mint", insert.Identity.Condition);
            Assert.Equal("English", insert.Identity.Language);
            Assert.Equal("nonfoil", insert.Identity.Finish);
            Assert.Null(insert.Identity.LocationId);
            Assert.Null(insert.Identity.Comment);

            Assert.Equal(3, insert.CardsOwned);
            Assert.Equal(1, insert.CardsForTrade);

            var survivor = Assert.Single(plan.ChangeSet.AddedOrUpdated);
            Assert.Equal(3, survivor.CardsOwned);
            Assert.Equal(1, survivor.CardsForTrade);
        }
        [Fact]
        public void PlanIdentityRewriteBatch_NewRowsSameCardDifferentComment_CreatesSeparateInserts()
        {
            var snapshot = new EmptySnapshot();
            var logic = new CollectionMutationsLogic();

            var cardA = new CardSet
            {
                Uuid = "foo-uuid",
                SelectedCondition = "Near Mint",
                SelectedFinish = "nonfoil",
                Language = "English",
                Comment = "signed",
                CardsOwned = 1,
                CardsForTrade = 0
            };

            var cardB = new CardSet
            {
                Uuid = "foo-uuid",
                SelectedCondition = "Near Mint",
                SelectedFinish = "nonfoil",
                Language = "English",
                Comment = "altered",
                CardsOwned = 1,
                CardsForTrade = 0
            };

            var plan = logic.PlanIdentityRewriteBatch([cardA, cardB], snapshot);

            Assert.Empty(plan.DeleteIds);
            Assert.Empty(plan.Updates);
            Assert.Equal(2, plan.Inserts.Count);

            Assert.Contains(plan.Inserts, x => x.Identity.Comment == "signed");
            Assert.Contains(plan.Inserts, x => x.Identity.Comment == "altered");

            Assert.Equal(2, plan.ChangeSet.AddedOrUpdated.Count);
        }
        [Fact]
        public void PlanIdentityRewriteBatch_NewRowsSameCardDifferentLocation_CreatesSeparateInserts()
        {
            var snapshot = new EmptySnapshot();
            var logic = new CollectionMutationsLogic();

            var cardA = new CardSet
            {
                Uuid = "foo-uuid",
                SelectedCondition = "Near Mint",
                SelectedFinish = "nonfoil",
                Language = "English",
                SelectedLocationId = 10,
                CardsOwned = 1,
                CardsForTrade = 0
            };

            var cardB = new CardSet
            {
                Uuid = "foo-uuid",
                SelectedCondition = "Near Mint",
                SelectedFinish = "nonfoil",
                Language = "English",
                SelectedLocationId = 20,
                CardsOwned = 1,
                CardsForTrade = 0
            };

            var plan = logic.PlanIdentityRewriteBatch([cardA, cardB], snapshot);

            Assert.Empty(plan.DeleteIds);
            Assert.Empty(plan.Updates);
            Assert.Equal(2, plan.Inserts.Count);

            Assert.Contains(plan.Inserts, x => x.Identity.LocationId == 10);
            Assert.Contains(plan.Inserts, x => x.Identity.LocationId == 20);

            Assert.Equal(2, plan.ChangeSet.AddedOrUpdated.Count);
        }
        [Fact]
        public void PlanIdentityRewriteBatch_ExistingRowLocationClearedToNull_WhenNullIdentityExists_Merges()
        {
            var nullLocationIdentity = new CollectionIdentity(
                "foo-uuid",
                "Near Mint",
                "English",
                "nonfoil",
                null,
                null);

            var locatedIdentity = new CollectionIdentity(
                "foo-uuid",
                "Near Mint",
                "English",
                "nonfoil",
                10,
                null);

            var snapshot = new TestSnapshot(
                rows:
                [
                    new MyCollectionRow
            {
                CardId = 100,
                Identity = nullLocationIdentity,
                CardsOwned = 2,
                CardsForTrade = 1
            },
            new MyCollectionRow
            {
                CardId = 200,
                Identity = locatedIdentity,
                CardsOwned = 3,
                CardsForTrade = 1
            }
                ]);

            var logic = new CollectionMutationsLogic();

            var editedCard = new CardSet
            {
                CardId = 200,
                Uuid = "foo-uuid",
                SelectedCondition = "Near Mint",
                SelectedFinish = "nonfoil",
                Language = "English",
                SelectedLocationId = null,
                CardsOwned = 3,
                CardsForTrade = 1
            };

            var plan = logic.PlanIdentityRewriteBatch([editedCard], snapshot);

            Assert.Equal([200], plan.DeleteIds);
            Assert.Empty(plan.Inserts);

            var update = Assert.Single(plan.Updates);
            Assert.Equal(100, update.CardId);
            Assert.Equal(nullLocationIdentity, update.Identity);
            Assert.Equal(5, update.CardsOwned);
            Assert.Equal(2, update.CardsForTrade);

            Assert.Equal([200], plan.ChangeSet.RemovedIds);

            var survivor = Assert.Single(plan.ChangeSet.AddedOrUpdated);
            Assert.Equal(100, survivor.CardId);
            Assert.Null(survivor.SelectedLocationId);
            Assert.Equal(5, survivor.CardsOwned);
            Assert.Equal(2, survivor.CardsForTrade);
        }
        [Fact]
        public void PlanIdentityRewriteBatch_MixedExistingAndNewRows_CanUpdateAndInsertInSameBatch()
        {
            var existingIdentity = new CollectionIdentity(
                "existing-uuid",
                "Near Mint",
                "English",
                "nonfoil",
                null,
                null);

            var snapshot = new TestSnapshot(
                rows:
                [
                    new MyCollectionRow
            {
                CardId = 100,
                Identity = existingIdentity,
                CardsOwned = 2,
                CardsForTrade = 0
            }
                ]);

            var logic = new CollectionMutationsLogic();

            var existingEdit = new CardSet
            {
                CardId = 100,
                Uuid = "existing-uuid",
                SelectedCondition = "Near Mint",
                SelectedFinish = "nonfoil",
                Language = "English",
                CardsOwned = 3,
                CardsForTrade = 1
            };

            var newSplitRow = new CardSet
            {
                CardId = null,
                Uuid = "new-uuid",
                SelectedCondition = "Near Mint",
                SelectedFinish = "nonfoil",
                Language = "English",
                CardsOwned = 1,
                CardsForTrade = 0
            };

            var plan = logic.PlanIdentityRewriteBatch([existingEdit, newSplitRow], snapshot);

            Assert.Empty(plan.DeleteIds);

            var update = Assert.Single(plan.Updates);
            Assert.Equal(100, update.CardId);
            Assert.Equal(3, update.CardsOwned);
            Assert.Equal(1, update.CardsForTrade);

            var insert = Assert.Single(plan.Inserts);
            Assert.Equal("new-uuid", insert.Identity.Uuid);
            Assert.Equal(1, insert.CardsOwned);
            Assert.Equal(0, insert.CardsForTrade);

            Assert.Empty(plan.ChangeSet.RemovedIds);
            Assert.Equal(2, plan.ChangeSet.AddedOrUpdated.Count);
        }
    }
}
