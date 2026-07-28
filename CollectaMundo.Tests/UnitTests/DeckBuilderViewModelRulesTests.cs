using CollectaMundo.ApplicationServices.Decks;
using CollectaMundo.ApplicationServices.Shared.UnitOfWork;
using CollectaMundo.DomainLogic.Decks;
using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.Infrastructure.Decks;
using CollectaMundo.Tests.TestUtils;
using CollectaMundo.ViewModels.CardLists;
using CollectaMundo.ViewModels.Decks;
using CollectaMundo.ViewModels.Filtering;
using Moq;
using System.Data.SQLite;

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
        private static CommanderTestSuite CreateSuite(string format)
        {
            var unitOfWorkRunnerMock = new Mock<IUnitOfWorkRunner>(MockBehavior.Strict);
            var repositoryMock = new Mock<IDeckBuilderRepo>(MockBehavior.Strict);
            var deckBuilderLogic = new DeckBuilderLogic();
            var deckBuilderService = new DeckBuilderService(unitOfWorkRunnerMock.Object,deckBuilderLogic,repositoryMock.Object);
            var viewModel = CreateDeckBuilderViewModel(deckBuilderService);

            viewModel.DeckLocationId = 42;
            viewModel.DeckFormat = format;

            return new CommanderTestSuite(viewModel);
        }
        private static DeckBuilderViewModel CreateDeckBuilderViewModel(IDeckBuilderService deckBuilderService)
        {
            var oracleCardsViewModel = Mock.Of<CardListViewModel<OracleCard>>();
            var filterPanelViewModel = Mock.Of<FilterPanelViewModel>();
            return new DeckBuilderViewModel(deckBuilderService, oracleCardsViewModel, filterPanelViewModel);
        }
        private sealed record CommanderTestSuite(DeckBuilderViewModel ViewModel);
    }
}
