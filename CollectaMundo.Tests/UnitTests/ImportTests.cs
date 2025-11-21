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
                ["Card Name"] = name
            };

            if (!string.IsNullOrWhiteSpace(setCode))
            {
                fields["Set Code"] = setCode;
            }

            if (!string.IsNullOrWhiteSpace(setName))
            {
                fields["Set Name"] = setName;
            }

            return new TempCardItem { Fields = fields };
        }
        private static IReadOnlyList<NameSetColumnMapping> BuildMappings(bool includeSetCode = true, bool includeSetName = false)
        {
            // Determine which CSV headers should exist
            // If SetName is used, include it in CsvHeaders list
            var headers = new List<string> { "Card Name" };
            if (includeSetCode)
            {
                headers.Add("Set Code");
            }

            if (includeSetName)
            {
                headers.Add("Set Name");
            }

            var list = new List<NameSetColumnMapping>
            {
                // Card Name mapping (always required)
                new()
                {
                    FieldToMap = "Card Name",
                    SelectedCsvHeader = "Card Name",
                    CsvHeaders = headers
                },

                // Set Code mapping (optional)
                new()
                {
                    FieldToMap = "Set Code",
                    SelectedCsvHeader = includeSetCode ? "Set Code" : null,
                    CsvHeaders = headers
                },

                // Set Name mapping (optional)
                new()
                {
                    FieldToMap = "Set Name",
                    SelectedCsvHeader = includeSetName ? "Set Name" : null,
                    CsvHeaders = headers
                }
            };

            return list;
        }

        private async Task<(IReadOnlyList<TempCardItem> Items, ImportMatchSummaryDto Result)> RunStep3Async(IReadOnlyList<TempCardItem> items, IReadOnlyList<NameSetColumnMapping>? mappings = null)
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

    }
}
