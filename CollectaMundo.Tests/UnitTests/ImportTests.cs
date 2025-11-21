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


        // -------------------------------------------------------------
        // TEST 1: Single Match (Name + SetCode)
        // -------------------------------------------------------------
        [Fact]
        public async Task Step3_SingleMatch_ReturnsUuidAndNoMultipleUuids()
        {
            // Arrange
            // Real card known to exist in your seeded DB
            var item = new TempCardItem
            {
                Fields = new Dictionary<string, string>
                {
                    ["Card Name"] = "Snapping Sailback",
                    ["Set Code"] = "PLST"
                }
            };

            var importList = new List<TempCardItem> { item };

            // Create NameSetColumnMappings
            var mappings = new List<NameSetColumnMapping>
            {
                new() {
                    FieldToMap = "Card Name",
                    SelectedCsvHeader = "Card Name",
                    CsvHeaders = ["Card Name", "Set Code"]
                },
                new() {
                    FieldToMap = "Set Code",
                    SelectedCsvHeader = "Set Code",
                    CsvHeaders = ["Card Name", "Set Code"]
                },
                new() {
                    FieldToMap = "Set Name",
                    SelectedCsvHeader = null,   // Not used in this scenario
                    CsvHeaders = ["Card Name", "Set Code"]
            }
        };

            // Progress sinks stub
            var progress = ProgressSinks.NoOp;

            // Act
            var result = await _service.TryResolveUuidsFromNameAndSetAsync(
                importList,
                mappings,
                progress,
                token: CancellationToken.None
            );

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0, result.ItemsWithMultipleUuids);

            Assert.True(item.Fields.ContainsKey("uuid"), "uuid field must be populated");
            Assert.False(item.Fields.ContainsKey("uuids"), "uuids field must NOT be present");

            string uuid = item.Fields["uuid"];
            Assert.False(string.IsNullOrWhiteSpace(uuid));
        }

    }
}
