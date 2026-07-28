using CollectaMundo.ApplicationServices.Decks;
using CollectaMundo.ApplicationServices.Shared.UnitOfWork;
using CollectaMundo.DomainLogic.Decks;
using CollectaMundo.Infrastructure.Decks;
using CollectaMundo.Tests.TestUtils;
using CollectaMundo.ViewModels.Decks;
using Moq;

namespace CollectaMundo.Tests.UnitTests
{
    public class DeckBuilderViewModelRulesTests
    {
        [Fact]
        public void SelectingLegendaryCreature_InNonCommanderFormat_HidesCommanderButton()
        {
            var sut = CreateSuite(format: "standard");
            var card = TestCardFactory.CreateLegendaryCreature();
            sut.ViewModel.SelectedOracleCard = card;
            Assert.False(sut.ViewModel.CanSetSelectedOracleCardAsCommander);
        }

        [Fact]
        public void SelectingLegendaryCreature_InCommanderFormat_ShowsCommanderButton()
        {
            var sut = CreateSuite(format: "commander");
            var card = TestCardFactory.CreateLegendaryCreature();

            sut.ViewModel.SelectedOracleCard = card;

            Assert.True(sut.ViewModel.CanSetSelectedOracleCardAsCommander);
        }
        private static CommanderTestSuite CreateSuite(string format)
        {
            var unitOfWorkRunnerMock = new Mock<IUnitOfWorkRunner>(MockBehavior.Strict);
            var repositoryMock = new Mock<IDeckBuilderRepo>(MockBehavior.Strict);
            var deckBuilderLogic = new DeckBuilderLogic();
            var deckBuilderService = new DeckBuilderService(unitOfWorkRunnerMock.Object, deckBuilderLogic, repositoryMock.Object);

            var viewModel = new DeckBuilderViewModel(deckBuilderService, null!, null!)
            {
                DeckLocationId = 42,
                DeckFormat = format
            };

            return new CommanderTestSuite(viewModel);
        }
        private sealed record CommanderTestSuite(DeckBuilderViewModel ViewModel);
    }
}
