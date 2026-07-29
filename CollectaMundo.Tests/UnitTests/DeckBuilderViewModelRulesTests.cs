using CollectaMundo.ApplicationServices.Decks;
using CollectaMundo.ApplicationServices.Shared.UnitOfWork;
using CollectaMundo.DomainLogic.Decks;
using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.Infrastructure.Decks;
using CollectaMundo.Tests.TestUtils;
using CollectaMundo.ViewModels.Decks;
using CollectaMundo.ViewModels.Decks.Models;
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

        [Fact]
        public void SelectingLegendaryCreature_InCommanderFormat_ShowsCommanderButton()
        {
            var sut = CreateSuite(format: "commander");
            var card = TestCardFactory.CreateLegendaryCreature();

            sut.ViewModel.SelectedOracleCard = card;

            Assert.True(sut.ViewModel.CanSetSelectedOracleCardAsCommander);
        }

        [Fact]
        public void SelectingNonLegendaryCreature_InCommanderFormat_HidesCommanderButton()
        {
            var sut = CreateSuite(format: "commander");
            var card = TestCardFactory.CreatePrinting(
                uuid: "printing-normal-creature",
                oracleId: "oracle-normal-creature",
                name: "Ordinary Creature",
                types: "Creature",
                superTypes: string.Empty,
                subTypes: "Human",
                type: "Creature — Human")
                .Oracle;

            sut.ViewModel.SelectedOracleCard = card;

            Assert.False(sut.ViewModel.CanSetSelectedOracleCardAsCommander);
        }

        [Theory]
        [InlineData("This card can be your commander.")]
        [InlineData("This card can be a commander.")]
        public void SelectingCardWhoseRulesTextAllowsCommander_ShowsCommanderButton(string rulesText)
        {
            var sut = CreateSuite(format: "commander");

            var card = TestCardFactory.CreatePrinting(
                uuid: "printing-rules-commander",
                oracleId: "oracle-rules-commander",
                name: "Rules Commander",
                types: "Creature",
                superTypes: string.Empty,
                subTypes: "Human",
                type: "Creature — Human",
                text: rulesText)
                .Oracle;

            sut.ViewModel.SelectedOracleCard = card;

            Assert.True(sut.ViewModel.CanSetSelectedOracleCardAsCommander);
        }

        [Fact]
        public void SelectingBackground_InCommanderFormat_ShowsCommanderButton()
        {
            var sut = CreateSuite(format: "commander");
            var card = TestCardFactory.CreateBackground();

            sut.ViewModel.SelectedOracleCard = card;

            Assert.True(sut.ViewModel.CanSetSelectedOracleCardAsCommander);
        }

        [Fact]
        public void ClearingSelectedOracleCard_HidesCommanderButton()
        {
            var sut = CreateSuite(format: "commander");
            var card = TestCardFactory.CreateLegendaryCreature();

            sut.ViewModel.SelectedOracleCard = card;

            Assert.True(sut.ViewModel.CanSetSelectedOracleCardAsCommander);
            sut.ViewModel.SelectedOracleCard = null;
            Assert.False(sut.ViewModel.CanSetSelectedOracleCardAsCommander);
        }

        [Fact]
        public void SelectingOracleCard_ShowsAddButton()
        {
            var sut = CreateSuite(format: "standard");
            var card = TestCardFactory.CreateLegendaryCreature();

            sut.ViewModel.SelectedOracleCard = card;

            Assert.True(sut.ViewModel.IsAddButtonVisible);
        }

        [Fact]
        public async Task SettingEligibleCardAsCommander_AddsCardToCommanderZone()
        {
            var sut = CreateSuite(format: "commander");
            var card = TestCardFactory.CreateLegendaryCreature();

            sut.ViewModel.SelectedOracleCard = card;

            await sut.ViewModel.SetOracleCardAsCommanderCommand.ExecuteAsync(card);

            var commander = Assert.Single(sut.ViewModel.CommanderZone.Cards);

            Assert.Equal(card.ScryfallOracleId, commander.OracleId);

            Assert.True(sut.ViewModel.IsCommanderZoneVisible);
        }

        [Fact]
        public async Task SelectingSameCommanderTwice_MakesCommanderNotSettable()
        {
            var sut = CreateSuite(format: "commander");
            var card = TestCardFactory.CreateLegendaryCreature();

            // Select and set commander
            await SetCommanderAsync(sut.ViewModel, card);

            // Delect card
            sut.ViewModel.SelectedOracleCard = null;

            // Select same card again
            sut.ViewModel.SelectedOracleCard = card;

            Assert.False(sut.ViewModel.CanSetSelectedOracleCardAsCommander);
        }

        [Fact]
        public async Task SettingSecondNormalCommander_ReplacesExistingCommander()
        {
            var sut = CreateSuite(format: "commander");

            var first = TestCardFactory.CreateLegendaryCreature(
                oracleId: "oracle-first",
                name: "First Commander");

            var second = TestCardFactory.CreateLegendaryCreature(
                oracleId: "oracle-second",
                name: "Second Commander");

            await SetCommanderAsync(sut.ViewModel, first);

            sut.ViewModel.SelectedOracleCard = second;

            Assert.True(
                sut.ViewModel.CanSetSelectedOracleCardAsCommander);

            await sut.ViewModel
                .SetOracleCardAsCommanderCommand
                .ExecuteAsync(second);

            var commander = Assert.Single(
                sut.ViewModel.CommanderZone.Cards);

            Assert.Equal("oracle-second", commander.OracleId);
        }

        [Theory]
        [InlineData("Partner")]
        [InlineData("Partner with")]
        [InlineData("Friends forever")]
        [InlineData("Doctor's Companion")]
        [InlineData("Choose a Background")]
        public async Task SettingPairingCapableCardAsSecondCommander_AddsCard(string keywords)
        {
            var sut = CreateSuite(format: "commander");

            var first = TestCardFactory.CreateLegendaryCreature(oracleId: "oracle-first", name: "First Commander");
            var partner = TestCardFactory.CreateLegendaryCreature(oracleId: "oracle-partner", name: "Partner Commander", keywords: keywords);

            await SetCommanderAsync(sut.ViewModel, first);

            sut.ViewModel.SelectedOracleCard = partner;

            Assert.True(sut.ViewModel.CanSetSelectedOracleCardAsCommander);

            await sut.ViewModel.SetOracleCardAsCommanderCommand.ExecuteAsync(partner);

            Assert.Equal(2, sut.ViewModel.CommanderZone.Cards.Count);
        }

        [Fact]
        public async Task ExistingPartner_AllowsNormalSecondCommander()
        {
            var sut = CreateSuite(format: "commander");

            var partner = TestCardFactory.CreateLegendaryCreature(oracleId: "oracle-partner", name: "Partner Commander", keywords: "Partner");

            var normalLegend = TestCardFactory.CreateLegendaryCreature(
                oracleId: "oracle-normal",
                name: "Normal Legend");

            await SetCommanderAsync(sut.ViewModel, partner);

            sut.ViewModel.SelectedOracleCard = normalLegend;

            Assert.True(
                sut.ViewModel.CanSetSelectedOracleCardAsCommander);

            await sut.ViewModel
                .SetOracleCardAsCommanderCommand
                .ExecuteAsync(normalLegend);

            Assert.Equal(
                2,
                sut.ViewModel.CommanderZone.Cards.Count);
        }

        [Fact]
        public async Task SettingThirdCommander_ReplacesExistingCommanderPair()
        {
            var sut = CreateSuite(format: "commander");

            var first = TestCardFactory.CreateLegendaryCreature(
                oracleId: "oracle-first",
                name: "First",
                keywords: "Partner");

            var second = TestCardFactory.CreateLegendaryCreature(
                oracleId: "oracle-second",
                name: "Second",
                keywords: "Partner");

            var third = TestCardFactory.CreateLegendaryCreature(
                oracleId: "oracle-third",
                name: "Third",
                keywords: "Partner");

            await SetCommanderAsync(sut.ViewModel, first);
            await SetCommanderAsync(sut.ViewModel, second);

            Assert.Equal(
                2,
                sut.ViewModel.CommanderZone.Cards.Count);

            await SetCommanderAsync(sut.ViewModel, third);

            var commander = Assert.Single(
                sut.ViewModel.CommanderZone.Cards);

            Assert.Equal("oracle-third", commander.OracleId);
        }

        #region Companion Tests

        [Fact]
        public void SelectingCardWithoutCompanionKeyword_HidesCompanionButton()
        {
            var sut = CreateSuite(format: "commander");
            var card = TestCardFactory.CreateLegendaryCreature();

            sut.ViewModel.SelectedOracleCard = card;

            Assert.False(sut.ViewModel.CanSetSelectedOracleCardAsCompanion);
        }

        [Fact]
        public void SelectingCompanionCard_ShowsCompanionButton()
        {
            var sut = CreateSuite(format: "standard");

            var card = TestCardFactory.CreatePrinting(
                uuid: "printing-companion",
                oracleId: "oracle-companion",
                name: "Test Companion",
                types: "Creature",
                keywords: "Companion")
                .Oracle;

            sut.ViewModel.SelectedOracleCard = card;

            Assert.True(sut.ViewModel.CanSetSelectedOracleCardAsCompanion);
        }

        #endregion

        #region Image request tests

        [Fact]
        public void SelectingOracleCard_RaisesCardImageSelectionRequest()
        {
            var sut = CreateSuite(format: "commander");
            var card = TestCardFactory.CreateLegendaryCreature();

            OracleCardImageSelectionRequest? actual = null;

            sut.ViewModel.CardImageSelectionRequested += (_, request) => actual = request;

            sut.ViewModel.SelectedOracleCard = card;

            Assert.NotNull(actual);
            Assert.Equal(card.ScryfallOracleId, actual.OracleId);
            Assert.Equal(card.Name, actual.Name);
        }

        [Fact]
        public void ClearingOracleCardSelection_RaisesEmptyImageRequest()
        {
            var sut = CreateSuite(format: "commander");
            var card = TestCardFactory.CreateLegendaryCreature();

            OracleCardImageSelectionRequest? actual = null;

            sut.ViewModel.SelectedOracleCard = card;
            sut.ViewModel.CardImageSelectionRequested += (_, request) => actual = request;
            sut.ViewModel.SelectedOracleCard = null;

            Assert.NotNull(actual);
            Assert.Null(actual.OracleId);
        }

        #endregion

        #region Helpers
        private static async Task SetCommanderAsync(DeckBuilderViewModel viewModel, OracleCard card)
        {
            viewModel.SelectedOracleCard = card;
            Assert.True(viewModel.CanSetSelectedOracleCardAsCommander);
            await viewModel.SetOracleCardAsCommanderCommand.ExecuteAsync(card);
        }
        private static CommanderTestSuite CreateSuite(string format)
        {
            var unitOfWorkRunnerMock = new Mock<IUnitOfWorkRunner>(MockBehavior.Strict);
            var repositoryMock = new Mock<IDeckBuilderRepo>(MockBehavior.Strict);
            repositoryMock.Setup(repo => repo.ReplaceDeckAsync(It.IsAny<SQLiteConnection>(), It.IsAny<SQLiteTransaction>(), It.IsAny<int>(), It.IsAny<IReadOnlyCollection<DeckCardEntry>>())).Returns(Task.CompletedTask);

            unitOfWorkRunnerMock.Setup(runner => runner.ExecuteWriteAsync<bool>(It.IsAny<Func<SQLiteConnection, SQLiteTransaction, Task<(bool Result, bool Commit)>>>()))
                .Returns(async (Func<SQLiteConnection, SQLiteTransaction, Task<(bool Result, bool Commit)>> action) =>
                {
                    var (result, _) = await action(null!, null!);
                    return result;
                });

            var deckBuilderLogic = new DeckBuilderLogic();
            var deckBuilderService = new DeckBuilderService(unitOfWorkRunnerMock.Object, deckBuilderLogic, repositoryMock.Object);
            var viewModel = new DeckBuilderViewModel(deckBuilderService, null!, null!)
            {
                DeckLocationId = 42,
                DeckFormat = format
            };

            return new CommanderTestSuite(viewModel, unitOfWorkRunnerMock, repositoryMock);
        }
        private sealed record CommanderTestSuite(DeckBuilderViewModel ViewModel, Mock<IUnitOfWorkRunner> UnitOfWorkRunnerMock, Mock<IDeckBuilderRepo> RepositoryMock);

        #endregion
    }
}
