using CollectaMundo.ApplicationServices.Shared;
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
            importVM.ImportCardList.Should().NotBeEmpty();



            // =====================================================
            // Step 2 – ID column mapping
            // =====================================================

            var step2 = (ImportStep02_IdMappingViewModel)importVM.CurrentStepViewModel;
            importVM.CurrentStepViewModel.Should().BeOfType<ImportStep02_IdMappingViewModel>();
            importVM.ProgressStep.Should().Be("ID column mapping");
            step2.PrimaryActionButtonText.Should().Contain("Proceed");


            // =====================================================
            // Step 3 – Name & set mapping
            // =====================================================


            // =====================================================
            // ...
            // Continue same pattern up to Step 9
            // =====================================================
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
