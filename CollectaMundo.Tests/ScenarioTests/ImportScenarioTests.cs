using CollectaMundo.ApplicationServices.Shared;
using CollectaMundo.DomainLogic.Import.Models;
using CollectaMundo.Infrastructure.Shared;
using CollectaMundo.Tests.TestUtils;
using CollectaMundo.ViewModels;
using CollectaMundo.ViewModels.Import.ImportSteps;
using FluentAssertions;
using System.IO;

namespace CollectaMundo.Tests.ScenarioTests
{

    public sealed class ImportScenarioTests(InMemoryDatabaseFixture fx) : IClassFixture<InMemoryDatabaseFixture>, IAsyncLifetime
    {
        #region Test class setup
        private readonly InMemoryDatabaseFixture _fx = fx;

        private IDbConnectionFactory _dbFactory = null!;
        private MainWindowViewModel _mainVM = null!;
        public async ValueTask InitializeAsync()
        {
            _dbFactory = SharedMemoryDbFactory.CreateInMemoryDbFactory(_fx.DbName);
            (_mainVM, _) = await TestAppBuilder.BuildAsync(_fx, _dbFactory);
        }
        public ValueTask DisposeAsync()
        {
            _mainVM.Dispose();
            return ValueTask.CompletedTask;
        }
        #endregion

        [Fact]
        public async Task Import_full_flow_happy_path()
        {
            // =====================================================
            // Arrange – infrastructure & initial state
            // =====================================================

            var csvPath = Path.Combine(AppContext.BaseDirectory, "TestResources/ImportTestCsvFiles", "ImportTest1.csv");

            File.Exists(csvPath).Should().BeTrue();

            var prompt = new TestPromptService(csvPath);
            var picker = new TestFileSystemPicker(csvPath);

            var (vm, _) = await TestAppBuilder.BuildAsync(_fx, _dbFactory, eventSink: null, promptOverride: prompt, filePickerOverride: picker);

            _mainVM = vm;
            var importVM = _mainVM.ImportVM;

            _mainVM.AllCardsVM.Cards.Should().NotBeNullOrEmpty();
            _mainVM.MyCollectionVM.Cards.Should().NotBeNull();

            // =====================================================
            // Step 0 – Begin wizard
            // =====================================================

            await importVM.Begin();
            var step1 = importVM.CurrentStepViewModel.Should().BeOfType<ImportStep01_StartViewModel>().Subject;

            // =====================================================
            // Step 1 – Parse CSV & move to ID mapping
            // =====================================================

            importVM.CurrentStepViewModel.Should().BeOfType<ImportStep01_StartViewModel>();
            importVM.ProgressHeadline.Should().Be("The Import Wizard");
            step1.PrimaryActionButtonText.Should().Contain("Let's go");

            var step1Result = await step1.OnPrimaryAction(); // Parse CSV

            // Assert step 1 completed successfully
            step1Result.Code.Should().Be(OperationResultCode.Success);
            importVM.ImportCardList.Should().HaveCount(8);

            // =====================================================
            // Step 2 – ID column mapping
            // =====================================================

            var step2 = (ImportStep02_IdMappingViewModel)importVM.CurrentStepViewModel;
            importVM.CurrentStepViewModel.Should().BeOfType<ImportStep02_IdMappingViewModel>();
            importVM.ProgressStep.Should().Be("ID column mapping");
            step2.PrimaryActionButtonText.Should().Contain("Proceed");

            // Assert CSV headers available
            step2.IdMappings.Should().HaveCount(1);
            var mapping = step2.IdMappings[0];

            mapping.CsvHeaders.Should().HaveCount(18);
            mapping.SelectedCsvHeader.Should().NotBeNull();
            mapping.SelectedDatabaseField.Should().NotBeNull();
            step2.CanExecutePrimaryAction.Should().BeTrue();

            // Simulate clearing mapping
            mapping.SelectedCsvHeader = null;
            mapping.SelectedDatabaseField = null;

            // Assert cleared state
            mapping.SelectedCsvHeader.Should().BeNull();
            mapping.SelectedDatabaseField.Should().BeNull();

            // CanExecute should now be false
            step2.CanExecutePrimaryAction.Should().BeFalse();

            // Map to MCM Id
            mapping.SelectedCsvHeader = "MCM ID";
            mapping.SelectedDatabaseField = "mcmId";

            // CanExecute should now be true
            step2.CanExecutePrimaryAction.Should().BeTrue();

            // Proceed to map using Id
            var step2Result = await step2.OnPrimaryAction();

            // Assert step 2 completed successfully
            step2Result.Code.Should().Be(OperationResultCode.Success);

            // After ID mapping, we should have 3 cards with UUIDs (the ones that had MCM IDs in the CSV)
            importVM.ImportCardList.Count(HasUuid).Should().Be(3);


            // =====================================================
            // Step 3 – Name & set mapping
            // =====================================================
            var step3 = (ImportStep03_NameSetMappingViewModel)importVM.CurrentStepViewModel;
            importVM.CurrentStepViewModel.Should().BeOfType<ImportStep03_NameSetMappingViewModel>();
            importVM.ProgressStep.Should().Be("Name and set mapping");
            step2.PrimaryActionButtonText.Should().Contain("Proceed");

            step3.NameSetMappings.Should().HaveCount(3);
            var nameSetmapping = step3.NameSetMappings;

            // Check CsvFieldsMappings object is correctly initialized with expected fields to map
            nameSetmapping[0].FieldToMap.Should().Be(ImportField.CardName);
            nameSetmapping[1].FieldToMap.Should().Be(ImportField.SetName);
            nameSetmapping[2].FieldToMap.Should().Be(ImportField.SetCode);
            nameSetmapping[0].CsvHeaders.Should().HaveCount(18);

            // Assert CSV headers pre-selected
            nameSetmapping[0].SelectedCsvHeader.Should().Be("CardName");
            nameSetmapping[1].SelectedCsvHeader.Should().Be("Set");
            nameSetmapping[2].SelectedCsvHeader.Should().Be("Set Code");

            // Proceed to map using Name & Set
            var step3Result = await step3.OnPrimaryAction();

            // Assert step 3 completed successfully
            step3Result.Code.Should().Be(OperationResultCode.Success);

            // After Name andSet mapping, we should have 3 cards with UUIDs (the ones that had MCM IDs in the CSV)
            importVM.ImportCardList.Count(HasUuid).Should().Be(6);
            importVM.ImportCardList.Count(HasUuids).Should().Be(1); // One card should have multiple UUIDs due to multiple matches

            // =====================================================
            // Step 4 – Multiple UUID matches
            // =====================================================
            var step4 = (ImportStep04_MultipleUuidsViewModel)importVM.CurrentStepViewModel;
            await EventuallyAsync(() => importVM.CurrentStepViewModel is ImportStep04_MultipleUuidsViewModel && importVM.ProgressStep == "Multiple versions found",
                timeout: TimeSpan.FromSeconds(3),
                because: "step 4 should be active and progress label updated");
            step4.PrimaryActionButtonText.Should().Contain("Proceed");

            // Check that MultipleUuidsItem object is correctly populated with the expected card that has multiple UUID matches
            step4.MultipleUuidItems.Should().HaveCount(1);
            step4.CanExecutePrimaryAction.Should().BeFalse();
            var multipleUuidItem = step4.MultipleUuidItems[0];
            multipleUuidItem.Name.Should().Contain("Prismatic Ending");
            multipleUuidItem.VersionedUuids.Should().HaveCount(2);
            multipleUuidItem.SelectedUuid.Should().BeNull();
            multipleUuidItem.VersionedUuids[0].DisplayText.Should().Be("Version 1");
            multipleUuidItem.VersionedUuids[1].DisplayText.Should().Be("Version 2");

            // Choose version 2 and proceed
            multipleUuidItem.SelectedUuid = "Version 2";
            step4.CanExecutePrimaryAction.Should().BeTrue();
            var step4Result = await step4.OnPrimaryAction();

            // Assert step 4 completed successfully
            step4Result.Code.Should().Be(OperationResultCode.Success);

            // After choosing the correct UUID for the card with multiple matches, we should now have 7 cards with UUIDs in total (the 3 from ID mapping + the 3 from Name/Set mapping + one we just resolved)
            importVM.ImportCardList.Count(HasUuid).Should().Be(7);
            importVM.ImportCardList.Count(HasUuids).Should().Be(0); // We should have resolved the multiple UUIDs, so none should have multiple anymore

            // =====================================================
            // Step 5 - Additional fields mapping
            // =====================================================
            var step5 = (ImportStep05_AdditionalFieldsMappingViewModel)importVM.CurrentStepViewModel;
            await EventuallyAsync(() => importVM.CurrentStepViewModel is ImportStep05_AdditionalFieldsMappingViewModel && importVM.ProgressStep == "Additional fields mapping",
                timeout: TimeSpan.FromSeconds(3),
                because: "step 5 should be active and progress label updated");
            step5.PrimaryActionButtonText.Should().Contain("Proceed");

            step5.AdditionalMappings.Should().HaveCount(5);
            var addtionalMappings = step5.AdditionalMappings;

            // Check CsvFieldsMappings object is correctly initialized with expected fields to map
            addtionalMappings[0].FieldToMap.Should().Be(ImportField.Condition);
            addtionalMappings[4].FieldToMap.Should().Be(ImportField.CardsForTrade);
            addtionalMappings[0].CsvHeaders.Should().HaveCount(18);

            // Assert CSV headers pre-selected
            addtionalMappings[0].SelectedCsvHeader.Should().Be("CardName");
            addtionalMappings[1].SelectedCsvHeader.Should().Be("Set");
            addtionalMappings[2].SelectedCsvHeader.Should().Be("Set Code");

            // =====================================================
            // ...
            // Continue same pattern up to Step 9
            // =====================================================
        }

        static bool HasUuid(TempCardItem item)
        {
            return item.CsvFields.TryGetValue("collectaMundoUuidImportField", out var uuid)
                   && !string.IsNullOrWhiteSpace(uuid);
        }

        static bool HasUuids(TempCardItem item)
        {
            return item.CsvFields.TryGetValue("collectaMundoUuidsImportField", out var uuid)
                   && !string.IsNullOrWhiteSpace(uuid);
        }
        private static async Task EventuallyAsync(Func<bool> condition, TimeSpan timeout, string? because = null)
        {
            var start = DateTime.UtcNow;

            while (DateTime.UtcNow - start < timeout)
            {
                if (condition())
                {
                    return;
                }

                await Task.Delay(10);
            }

            // One last check before failing (helps if it flips right at the end)
            condition().Should().BeTrue(because ?? "condition was not met before timeout");
        }
    }
    internal sealed class TestFileSystemPicker : IFileSystemPicker
    {
        private readonly string _pathToReturn;

        public TestFileSystemPicker(string pathToReturn)
        {
            _pathToReturn = pathToReturn;
        }

        public string? PickFile(
            string title,
            string filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*")
        {
            return _pathToReturn;
        }

        public string? PickFolder(string title, string? initialPath = null)
        {
            throw new NotSupportedException("PickFolder is not used in import tests.");
        }

        public string? PickSaveFile(string title, string defaultFileName, string filter)
        {
            throw new NotSupportedException("PickSaveFile is not used in import tests.");
        }
    }

}
