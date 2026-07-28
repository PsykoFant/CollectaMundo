using CollectaMundo.ApplicationServices.Decks;
using CollectaMundo.ApplicationServices.Shared.UnitOfWork;
using CollectaMundo.DomainLogic.Decks;
using CollectaMundo.DomainLogic.Decks.Models;
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
            var sut = CreateSut(format: "standard");
            var card = TestCardFactory.CreateLegendaryCreature();
            sut.ViewModel.SelectedOracleCard = card;
            Assert.False(sut.ViewModel.CanSetSelectedOracleCardAsCommander);
        }

        private static CommanderTestSuite CreateSut(string format)
        {
            var repositoryMock = new Mock<IDeckBuilderRepo>(MockBehavior.Strict);
            var unitOfWorkMock = new Mock<IUnitOfWork>(MockBehavior.Strict);
            var persistedDeck = new List<DeckCardEntry>();

            repositoryMock.Setup(repo => repo.ReplaceDeckAsync(
                    It.IsAny<int>(),
                    It.IsAny<IReadOnlyCollection<DeckCardEntry>>(),
                    It.IsAny<CancellationToken>()))
                .Callback<int,
                          IReadOnlyCollection<DeckCardEntry>,
                          CancellationToken>(
                    (_, entries, _) =>
                    {
                        persistedDeck.Clear();
                        persistedDeck.AddRange(entries);
                    })
                .Returns(Task.CompletedTask);

            unitOfWorkMock
                .Setup(unit => unit.ExecuteAsync(
                    It.IsAny<Func<CancellationToken, Task>>(),
                    It.IsAny<CancellationToken>()))
                .Returns<Func<CancellationToken, Task>,
                         CancellationToken>(
                    (operation, cancellationToken) =>
                        operation(cancellationToken));

            var logic = new DeckBuilderLogic();

            var service = new DeckBuilderService(
                logic,
                repositoryMock.Object,
                unitOfWorkMock.Object);

            var viewModel = CreateDeckBuilderViewModel(service);

            viewModel.DeckLocationId = 42;
            viewModel.DeckFormat = format;

            return new CommanderTestSuite(
                viewModel,
                repositoryMock,
                unitOfWorkMock);
        }
        private sealed record CommanderTestSuite(
    DeckBuilderViewModel ViewModel,
    Mock<IDeckBuilderRepo> RepositoryMock,
    Mock<IUnitOfWork> UnitOfWorkMock);
    }
}
