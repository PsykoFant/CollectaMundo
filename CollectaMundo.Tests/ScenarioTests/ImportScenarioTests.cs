using CollectaMundo.Infrastructure.Shared;
using CollectaMundo.Tests.TestUtils;
using CollectaMundo.ViewModels;
using CollectaMundo.ViewModels.Import.ImportSteps;
using FluentAssertions;

namespace CollectaMundo.Tests.ScenarioTests
{

    public sealed class ImportScenarioTests(InMemoryDatabaseFixture fx)
        : IClassFixture<InMemoryDatabaseFixture>, IAsyncLifetime
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
            var allCards = _mainVM.AllCardsVM.Cards;
            var myCollection = _mainVM.MyCollectionVM.Cards;

            allCards.Should().NotBeNullOrEmpty("we expect AllCardsVM to be hydrated");
            myCollection.Should().NotBeNull("MyCollectionVM should be initialized");

            // --- Start import ---
            var importVM = _mainVM.ImportVM;

            await importVM.Begin();

            // Assert that we landed in step 1
            importVM.CurrentStepViewModel.Should().BeOfType<ImportStep01_StartViewModel>();
            importVM.CurrentStepViewModel.PrimaryActionButtonText.Should().Contain("Let's go");
            importVM.ProgressHeadline.Should().Be("The Import Wizard");
        }
    }
}
