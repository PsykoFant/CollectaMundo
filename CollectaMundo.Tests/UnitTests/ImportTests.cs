using CollectaMundo.ApplicationServices.Import;
using CollectaMundo.ApplicationServices.Shared.Progress;
using CollectaMundo.DomainLogic.Import;
using CollectaMundo.DomainLogic.Import.Models;
using CollectaMundo.Infrastructure.Import;
using CollectaMundo.Tests.TestUtils;

namespace CollectaMundo.Tests.UnitTests
{
    public class ImportTests : IClassFixture<InMemoryDatabaseFixture>
    {
        private readonly InMemoryDatabaseFixture _fixture;
        private readonly ImportService _service;

        public ImportTests(InMemoryDatabaseFixture fixture)
        {
            _fixture = fixture;

            // Use SharedMemoryDbFactory to connect to the same in-memory DB as the fixture
            var dbFactory = SharedMemoryDbFactory.CreateInMemoryDbFactory(_fixture.DbName);

            _service = new ImportService(
                dbFactory,
                new ImportRepo(),
                fileSystemPicker: null!, // Not needed for service-level tests
                new ImportLogic()
            );
        }

        // ------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------
        private static TempCardItem MakeItemFull(string name, string? setCode = null, string? setName = null)
        {
            var fields = new Dictionary<string, string>
            {
                ["CardName"] = name
            };

            if (!string.IsNullOrWhiteSpace(setCode))
            {
                fields["SetCode"] = setCode;
            }

            if (!string.IsNullOrWhiteSpace(setName))
            {
                fields["SetName"] = setName;
            }

            return new TempCardItem { Fields = fields };
        }
        private static IReadOnlyList<TempCardItem> MakeItemsFull(params (string Name, string? SetCode, string? SetName)[] items)
        {
            return [.. items.Select(i => MakeItemFull(i.Name, i.SetCode, i.SetName))];
        }
        private static IReadOnlyList<CsvFieldMapping> BuildMappings(bool includeSetCode = true, bool includeSetName = false)
        {
            // Determine which CSV headers should exist
            // If SetName is used, include it in CsvHeaders list
            var headers = new List<string> { "CardName" };
            if (includeSetCode)
            {
                headers.Add("SetCode");
            }

            if (includeSetName)
            {
                headers.Add("SetName");
            }

            var list = new List<CsvFieldMapping>
            {
                // Card Name mapping (always required)
                new()
                {
                    FieldToMap = "CardName",
                    SelectedCsvHeader = "CardName",
                    CsvHeaders = headers
                },

                // Set Code mapping (optional)
                new()
                {
                    FieldToMap = "SetCode",
                    SelectedCsvHeader = includeSetCode ? "SetCode" : null,
                    CsvHeaders = headers
                },

                // Set Name mapping (optional)
                new()
                {
                    FieldToMap = "SetName",
                    SelectedCsvHeader = includeSetName ? "SetName" : null,
                    CsvHeaders = headers
                }
            };

            return list;
        }
        private static IReadOnlyList<TempCardItem> MakeStandardMixedStep3Items()
        {
            return MakeItemsFull(
                ("Viashino Runner", "USG", "Urza's Saga"),                                          // single uuid hit
                ("Prismatic Ending", "MH2", "Modern Horizons 2"),                                   // multi uuid hit
                ("realmwalker", "pkhm", null),                                                      // single uuid hit - also, test case insensitivity
                ("Thallid Devourer", null, "fallen empires"),                                       // single uuid hit - also, test case insensitivity
                ("bubbling cauldron", "ima", null),                                                 // single uuid hit - also, test case insensitivity
                ("Unblinking Observer // Unblinking Observer", null, "Midnight Hunt Art Series"),   // single uuid hit
                ("Zombie", "TM11", null),                                                           // single uuid hit
                ("jan jansen, chaos crafter", null, null),                                          // single uuid hit from token faceName - also, test case insensitivity
                ("Resurrection", "No Exist Code", "No Exist Name"),                                 // single uuid hit (fallback to name-only)
                ("Brisela, Voice of Nightmares", "No Exist Code", "No Exist Name"),                 // no uuid hit - we don't import meld backsides
                ("No Exist Card", "No Exist Code", "No Exist Name")                                 // no uuid hit
            );
        }
        private async Task<(IReadOnlyList<TempCardItem> Items, ImportMatchSummaryDto Result)> RunStep3Async(IReadOnlyList<TempCardItem> items, IReadOnlyList<CsvFieldMapping>? mappings = null)
        {
            mappings ??= BuildMappings(); // defaults: Name + SetCode

            var result = await _service.TryResolveUuidsFromNameAndSetAsync(
                items,
                mappings,
                ProgressSinks.NoOp,
                CancellationToken.None
            );

            return (items, result);
        }

        // ------------------------------------------------------------
        #region Step 3: Name + SetCode/SetName UUID resolution tests

        [Fact]
        public async Task Step3_SingleMatch_UsingSetCode_AssignsUuid()
        {
            // Act
            var (items, result) = await RunStep3Async(
                [MakeItemFull("Snapping Sailback", setCode: "PLST")],
                BuildMappings(includeSetCode: true, includeSetName: false)
            );

            var item = items[0];

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0, result.ItemsWithMultipleUuids);

            Assert.True(item.Fields.ContainsKey("uuid"), "uuid must be present for single match");
            Assert.False(item.Fields.ContainsKey("uuids"), "uuids must NOT be present for single match");
            Assert.False(string.IsNullOrWhiteSpace(item.Fields["uuid"]));
        }

        [Fact]
        public async Task Step3_SingleMatch_UsingSetName_AssignsUuid()
        {
            // Act
            var (items, result) = await RunStep3Async(
                [MakeItemFull("Font of Ire", setName: "Journey into Nyx")],
                BuildMappings(includeSetCode: false, includeSetName: true)
            );

            var item = items[0];

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0, result.ItemsWithMultipleUuids);

            Assert.True(item.Fields.ContainsKey("uuid"), "uuid must be present for single match");
            Assert.False(item.Fields.ContainsKey("uuids"), "uuids must NOT be present for single match");
            Assert.False(string.IsNullOrWhiteSpace(item.Fields["uuid"]));
        }

        [Fact]
        public async Task Step3_SingleMatch_UsingSetName_UsingSetCode_AssignsUuid()
        {
            // Act
            var (items, result) = await RunStep3Async(
                [MakeItemFull("Font of Ire", setCode: "JOU", setName: "Journey into Nyx")],
                BuildMappings(includeSetCode: true, includeSetName: true)
            );

            var item = items[0];

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0, result.ItemsWithMultipleUuids);

            Assert.True(item.Fields.ContainsKey("uuid"), "uuid must be present for single match");
            Assert.False(item.Fields.ContainsKey("uuids"), "uuids must NOT be present for single match");
            Assert.False(string.IsNullOrWhiteSpace(item.Fields["uuid"]));
        }

        [Fact]
        public async Task Step3_SingleMatch_UsingSetName_MissingOnSetCode_AssignsUuid()
        {
            // Act
            var (items, result) = await RunStep3Async(
                [MakeItemFull("Font of Ire", setCode: "BOGUS", setName: "Journey into Nyx")],
                BuildMappings(includeSetCode: true, includeSetName: true)
            );

            var item = items[0];

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0, result.ItemsWithMultipleUuids);

            Assert.True(item.Fields.ContainsKey("uuid"), "uuid must be present for single match");
            Assert.False(item.Fields.ContainsKey("uuids"), "uuids must NOT be present for single match");
            Assert.False(string.IsNullOrWhiteSpace(item.Fields["uuid"]));
        }

        [Fact]
        public async Task Step3_SingleMatch_UsingSetCode_MissingOnSetName_AssignsUuid()
        {
            // Act
            var (items, result) = await RunStep3Async(
                [MakeItemFull("Font of Ire", setCode: "JOU", setName: "BOGUS")],
                BuildMappings(includeSetCode: true, includeSetName: true)
            );

            var item = items[0];

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0, result.ItemsWithMultipleUuids);

            Assert.True(item.Fields.ContainsKey("uuid"), "uuid must be present for single match");
            Assert.False(item.Fields.ContainsKey("uuids"), "uuids must NOT be present for single match");
            Assert.False(string.IsNullOrWhiteSpace(item.Fields["uuid"]));
        }

        [Fact]
        public async Task Step3_MultiMatch_UsingSetCode_AssignsUuidsList()
        {
            // Act
            var (items, result) = await RunStep3Async(
                [MakeItemFull("Prismatic Ending", setCode: "MH2")],
                BuildMappings(includeSetCode: true, includeSetName: false)
            );

            var item = items[0];

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.ItemsWithMultipleUuids);

            Assert.False(item.Fields.ContainsKey("uuid"), "uuid must NOT be present for multi-match");
            Assert.True(item.Fields.ContainsKey("uuids"), "uuids must be present for multi-match");

            var raw = item.Fields["uuids"];
            Assert.False(string.IsNullOrWhiteSpace(raw), "uuids string must not be empty");

            var split = raw.Split(",", StringSplitOptions.RemoveEmptyEntries);
            Assert.True(split.Length > 1, "multi-match must contain more than 1 uuid");
        }

        [Fact]
        public async Task Step3_MultiMatch_UsingSetName_AssignsUuidsList()
        {
            // Act
            var (items, result) = await RunStep3Async(
                [MakeItemFull("Prismatic Ending", setName: "Modern Horizons 2")],
                BuildMappings(includeSetCode: false, includeSetName: true)
            );

            var item = items[0];

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.ItemsWithMultipleUuids);

            Assert.False(item.Fields.ContainsKey("uuid"), "uuid must NOT be present for multi-match");
            Assert.True(item.Fields.ContainsKey("uuids"), "uuids must be present for multi-match");

            var raw = item.Fields["uuids"];
            Assert.False(string.IsNullOrWhiteSpace(raw), "uuids string must not be empty");

            var split = raw.Split(",", StringSplitOptions.RemoveEmptyEntries);
            Assert.True(split.Length > 1, "multi-match must contain more than 1 uuid");
        }

        [Fact]
        public async Task Step3_NoMatch_UsingSetCode_UsingSetName_Throws()
        {
            // Arrange
            var items = new[] {
                MakeItemFull("No match", setName: "No set")
            };

            var mappings = BuildMappings(includeSetCode: true, includeSetName: true);

            // Act + Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await _service.TryResolveUuidsFromNameAndSetAsync(
                    items,
                    mappings,
                    ProgressSinks.NoOp,
                    CancellationToken.None
                );
            });
        }

        #endregion

        [Fact]
        public async Task Step3_MixedItems_UsingSetCode_UsingSetName_SingleMultiAndNone_AllowsNoneAndReturnsMultiSummary()
        {
            // Arrange
            var items = MakeStandardMixedStep3Items();

            var mappings = BuildMappings(includeSetCode: true, includeSetName: true);

            // Act
            var (processedItems, summary) = await RunStep3Async(items, mappings);

            // Assert
            Assert.NotNull(summary);

            // Summary:
            // Only Prismatic Ending should produce multiple UUIDs → exactly 1 multi-match
            Assert.Equal(1, summary.ItemsWithMultipleUuids);

            // Individual item checks
            foreach (var item in processedItems)
            {
                var name = item.Fields["CardName"];

                switch (name)
                {
                    case "Viashino Runner":
                    case "realmwalker":
                    case "Unblinking Observer // Unblinking Observer":
                    case "Thallid Devourer":
                    case "bubbling cauldron":
                    case "Resurrection":
                    case "jan jansen, chaos crafter":
                    case "Zombie":
                        // Single-match items must have uuid only
                        Assert.True(item.Fields.ContainsKey("uuid"), $"{name} should have uuid");
                        Assert.False(item.Fields.ContainsKey("uuids"), $"{name} should NOT have uuids");
                        break;

                    case "Prismatic Ending":
                        // Multi-match must have uuids only
                        Assert.True(item.Fields.ContainsKey("uuids"), $"{name} should have uuids");
                        Assert.False(item.Fields.ContainsKey("uuid"), $"{name} should NOT have uuid");

                        // Validate format: comma-separated multi UUIDs
                        var raw = item.Fields["uuids"];
                        Assert.False(string.IsNullOrWhiteSpace(raw));
                        var split = raw.Split(",", StringSplitOptions.RemoveEmptyEntries);
                        Assert.True(split.Length > 1, $"{name} should map to multiple UUIDs");
                        break;

                    case "No Exist Card":
                    case "Brisela, Voice of Nightmares":
                        // No-match items should have NEITHER uuid nor uuids
                        Assert.False(item.Fields.ContainsKey("uuid"), $"{name} should NOT have uuid");
                        Assert.False(item.Fields.ContainsKey("uuids"), $"{name} should NOT have uuids");
                        break;

                    default:
                        Assert.True(false, $"Unexpected card in test: {name}");
                        break;
                }
            }
        }

        [Fact]
        public async Task Step3_MixedItems_UsingSetCode_SingleMultiAndNone_AllowsNoneAndReturnsMultiSummary()
        {
            // Arrange
            var items = MakeStandardMixedStep3Items();

            var mappings = BuildMappings(includeSetCode: true, includeSetName: false);

            // Act
            var (processedItems, summary) = await RunStep3Async(items, mappings);

            // Assert
            Assert.NotNull(summary);

            // Summary:
            // Only Prismatic Ending should produce multiple UUIDs → exactly 1 multi-match
            Assert.Equal(1, summary.ItemsWithMultipleUuids);

            // Individual item checks
            foreach (var item in processedItems)
            {
                var name = item.Fields["CardName"];

                switch (name)
                {
                    case "Viashino Runner":
                    case "realmwalker":
                    case "Resurrection":
                    case "Unblinking Observer // Unblinking Observer":
                    case "Thallid Devourer":
                    case "bubbling cauldron":
                    case "jan jansen, chaos crafter":
                    case "Zombie":
                        // Single-match items must have uuid only
                        Assert.True(item.Fields.ContainsKey("uuid"), $"{name} should have uuid");
                        Assert.False(item.Fields.ContainsKey("uuids"), $"{name} should NOT have uuids");
                        break;

                    case "Prismatic Ending":
                        // Multi-match must have uuids only
                        Assert.True(item.Fields.ContainsKey("uuids"), $"{name} should have uuids");
                        Assert.False(item.Fields.ContainsKey("uuid"), $"{name} should NOT have uuid");

                        // Validate format: comma-separated multi UUIDs
                        var raw = item.Fields["uuids"];
                        Assert.False(string.IsNullOrWhiteSpace(raw));
                        var split = raw.Split(",", StringSplitOptions.RemoveEmptyEntries);
                        Assert.True(split.Length > 1, $"{name} should map to multiple UUIDs");
                        break;

                    case "No Exist Card":
                    case "Brisela, Voice of Nightmares":
                        // No-match items should have NEITHER uuid nor uuids
                        Assert.False(item.Fields.ContainsKey("uuid"), $"{name} should NOT have uuid");
                        Assert.False(item.Fields.ContainsKey("uuids"), $"{name} should NOT have uuids");
                        break;

                    default:
                        Assert.True(false, $"Unexpected card in test: {name}");
                        break;
                }
            }
        }

        [Fact]
        public async Task Step3_MixedItems_UsingSetName_SingleMultiAndNone_AllowsNoneAndReturnsMultiSummary()
        {
            // Arrange
            var items = MakeStandardMixedStep3Items();

            var mappings = BuildMappings(includeSetCode: false, includeSetName: true);

            // Act
            var (processedItems, summary) = await RunStep3Async(items, mappings);

            // Assert
            Assert.NotNull(summary);

            // Summary:
            // Only Prismatic Ending should produce multiple UUIDs → exactly 1 multi-match
            Assert.Equal(1, summary.ItemsWithMultipleUuids);

            // Individual item checks
            foreach (var item in processedItems)
            {
                var name = item.Fields["CardName"];

                switch (name)
                {
                    case "Viashino Runner":
                    case "realmwalker":
                    case "Zombie":
                    case "Thallid Devourer":
                    case "Resurrection":
                    case "bubbling cauldron":
                    case "jan jansen, chaos crafter":
                    case "Unblinking Observer // Unblinking Observer":
                        // Single-match items must have uuid only
                        Assert.True(item.Fields.ContainsKey("uuid"), $"{name} should have uuid");
                        Assert.False(item.Fields.ContainsKey("uuids"), $"{name} should NOT have uuids");
                        break;

                    case "Prismatic Ending":
                        // Multi-match must have uuids only
                        Assert.True(item.Fields.ContainsKey("uuids"), $"{name} should have uuids");
                        Assert.False(item.Fields.ContainsKey("uuid"), $"{name} should NOT have uuid");

                        // Validate format: comma-separated multi UUIDs
                        var raw = item.Fields["uuids"];
                        Assert.False(string.IsNullOrWhiteSpace(raw));
                        var split = raw.Split(",", StringSplitOptions.RemoveEmptyEntries);
                        Assert.True(split.Length > 1, $"{name} should map to multiple UUIDs");
                        break;

                    case "No Exist Card":
                    case "Brisela, Voice of Nightmares":
                        // No-match items should have NEITHER uuid nor uuids
                        Assert.False(item.Fields.ContainsKey("uuid"), $"{name} should NOT have uuid");
                        Assert.False(item.Fields.ContainsKey("uuids"), $"{name} should NOT have uuids");
                        break;

                    default:
                        Assert.True(false, $"Unexpected card in test: {name}");
                        break;
                }
            }
        }

        [Fact]
        public async Task Step3_ConflictingSetCodeAndSetName_StillYieldsMultiMatch()
        {
            // Arrange: conflicting SetName, correct SetCode
            var items = MakeItemsFull(
                ("Prismatic Ending", "MH2", "Modern Horizons")  // fake/non-matching name
            );

            var mappings = BuildMappings(includeSetCode: true, includeSetName: true);

            // Act
            var (processed, summary) = await RunStep3Async(items, mappings);

            // Assert
            Assert.NotNull(summary);

            // Because Prismatic Ending (MH2) is a multi-print card,
            // the fallback logic uses SetCode → multi-match
            Assert.Equal(1, summary.ItemsWithMultipleUuids);

            var card = processed[0];

            Assert.False(card.Fields.ContainsKey("uuid"));
            Assert.True(card.Fields.ContainsKey("uuids"));

            var raw = card.Fields["uuids"];
            Assert.False(string.IsNullOrWhiteSpace(raw));
            Assert.True(raw.Split(",", StringSplitOptions.RemoveEmptyEntries).Length > 1);
        }

        [Fact]
        public async Task Step3_NameOnly_MixedItems_HandlesSingleAndMultipleMatches()
        {
            // Arrange:
            // Name-only → no SetCode or SetName mappings
            var items = MakeItemsFull(
                ("Viashino Runner", "does not exist", "does not exist"),               // multi-match
                ("Jan Jansen, Chaos Crafter", "does not exist", "does not exist")      // single-match
            );

            // Only Card Name is mapped → triggers name-only fallback
            var mappings = BuildMappings(
                includeSetCode: true,
                includeSetName: true
            );

            // Act
            var (processed, summary) = await RunStep3Async(items, mappings);

            // Assert summary
            Assert.NotNull(summary);
            Assert.Equal(1, summary.ItemsWithMultipleUuids);   // exactly one multi-match item

            // ----- Viashino Runner → multi-match -----
            var runner = processed.First(i => i.Fields["CardName"] == "Viashino Runner");

            Assert.True(runner.Fields.ContainsKey("uuids"), "Runner must have multiple UUIDs.");
            Assert.False(runner.Fields.ContainsKey("uuid"), "Runner must not have single uuid.");

            var runnerRaw = runner.Fields["uuids"];
            Assert.False(string.IsNullOrWhiteSpace(runnerRaw));
            Assert.True(runnerRaw.Split(",", StringSplitOptions.RemoveEmptyEntries).Length > 1);

            // ----- Jan Jansen → single match -----
            var jj = processed.First(i => i.Fields["CardName"] == "Jan Jansen, Chaos Crafter");

            Assert.True(jj.Fields.ContainsKey("uuid"), "Jan Jansen must have single uuid.");
            Assert.False(jj.Fields.ContainsKey("uuids"), "Jan Jansen must not have multi uuid list.");
            Assert.False(string.IsNullOrWhiteSpace(jj.Fields["uuid"]));
        }

    }
}
