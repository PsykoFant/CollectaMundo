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
        public async Task Seeded_card_database_can_start_import_wizard()
        {
            var csvPath = Path.Combine("TestData", "sample.csv");
            var prompt = new TestPromptService(csvPath);

            (_mainVM, _) = await TestAppBuilder.BuildAsync(
                _fx,
                _dbFactory,
                eventSink: null,
                promptOverride: prompt);

            var importVM = _mainVM.ImportVM;

            var allCards = _mainVM.AllCardsVM.Cards;
            var myCollection = _mainVM.MyCollectionVM.Cards;

            allCards.Should().NotBeNullOrEmpty("we expect AllCardsVM to be hydrated");
            myCollection.Should().NotBeNull("MyCollectionVM should be initialized");

            await importVM.Begin();

            // Assert that we landed in step 1
            importVM.CurrentStepViewModel.Should().BeOfType<ImportStep01_StartViewModel>();
            importVM.CurrentStepViewModel.PrimaryActionButtonText.Should().Contain("Let's go");
            importVM.ProgressHeadline.Should().Be("The Import Wizard");
        }

        [Fact]
        public async Task Import_step1_parses_csv_and_moves_to_id_mapping()
        {
            var csvPath = Path.Combine(
                AppContext.BaseDirectory,
                "TestResources/ImportTestCsvFiles/",
                "ImportTest1.csv");

            File.Exists(csvPath).Should().BeTrue();

            var prompt = new TestPromptService(csvPath);
            var picker = new TestFileSystemPicker(csvPath);

            var (vm, _) = await TestAppBuilder.BuildAsync(
                _fx,
                _dbFactory,
                eventSink: null,
                promptOverride: prompt,
                filePickerOverride: picker);

            _mainVM = vm;

            var importVM = _mainVM.ImportVM;

            await importVM.Begin();
            importVM.CurrentStepViewModel.Should().BeOfType<ImportStep01_StartViewModel>();

            // ✅ THIS is the correct simulation of clicking "Let's go!"
            await importVM.PrimaryActionCommand.ExecuteAsync(null);

            // Assert we moved to step 2
            importVM.CurrentStepViewModel
                .Should().BeOfType<ImportStep02_IdMappingViewModel>();

            importVM.ImportCardList.Should().NotBeEmpty();
        }

    }

    internal sealed class TestFileSystemPicker(string pathToReturn) : IFileSystemPicker
    {
        private readonly string _pathToReturn = pathToReturn;

        public string? PickFile(string title) => _pathToReturn;

        public string? PickFile(string title, string filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*")
        {
            throw new NotImplementedException();
        }

        public string? PickFolder(string title, string? initialPath = null)
        {
            throw new NotImplementedException();
        }

        public string? PickSaveFile(string title, string defaultFileName, string filter)
        {
            throw new NotImplementedException();
        }
    }
}
