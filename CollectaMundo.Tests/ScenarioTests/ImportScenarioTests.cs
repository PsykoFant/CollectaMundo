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
