
using CollectaMundo.ApplicationServices.CardLists.Factories;
using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.Infrastructure.CardLists.Models;

namespace CollectaMundo.Tests.UnitTests
{
    public sealed class CardModelAndFactoryTests
    {
        [Fact]
        public void OracleCardFactory_Normalizes_CaseInsensitive_Duplicates()
        {
            var row = new OracleCardDbRow(
                ScryfallOracleId: "oracle-test",
                Name: "Test Card",
                GamePlayCard: 1,
                ManaCostRaw: null,
                ManaValue: null,
                Colors: "W, w, W",
                Keywords: "Flying, flying, FLYING",
                RulesText: null,
                SuperTypes: null,
                Types: "Creature, creature",
                SubTypes: null,
                Type: null);

            var card = OracleCardFactory.FromRow(row);

            Assert.Equal("Flying", card.Keywords);
            Assert.Equal("W", card.Colors);
            Assert.Equal("Creature", card.Types);
        }

        [Fact]
        public void PrintingCard_DelegatesOracleProperties()
        {
            var oracle = new OracleCard
            {
                ScryfallOracleId = "oracle-test",
                Name = "Test Card",
                Keywords = "Flying,Vigilance",
                Colors = "W",
                Types = "Creature",
                Text = "Oracle rules text",
                ManaValue = 4,
                GamePlayCard = 1
            };

            var printing = new PrintingCard
            {
                Uuid = "printing-1",
                Oracle = oracle,
                Side = "a",
                SetCode = "TST",
                Language = "English",
                Rarity = "rare"
            };

            Assert.Same(oracle, printing.Oracle);

            Assert.Equal("oracle-test", printing.ScryfallOracleId);
            Assert.Equal("Test Card", printing.Name);
            Assert.Equal("Flying,Vigilance", printing.Keywords);
            Assert.Equal("W", printing.Colors);
            Assert.Equal("Creature", printing.Types);
            Assert.Equal("Oracle rules text", printing.Text);
            Assert.Equal(4, printing.ManaValue);
        }

        [Fact]
        public void PrintingCards_SharingOracle_ReferenceSameOracleInstance()
        {
            var oracle = new OracleCard
            {
                ScryfallOracleId = "oracle-shared",
                Name = "Test Card",
                Keywords = "Flying",
                GamePlayCard = 1
            };

            var printing1 = new PrintingCard
            {
                Uuid = "printing-1",
                Oracle = oracle,
                SetCode = "SET1"
            };

            var printing2 = new PrintingCard
            {
                Uuid = "printing-2",
                Oracle = oracle,
                SetCode = "SET2"
            };

            Assert.NotEqual(printing1.Uuid, printing2.Uuid);

            Assert.Same(printing1.Oracle, printing2.Oracle);

            Assert.Equal("SET1", printing1.SetCode);
            Assert.Equal("SET2", printing2.SetCode);
        }

        [Fact]
        public void PrintingCard_Side_IsPhysicalPrintingProperty()
        {
            var oracle = new OracleCard
            {
                ScryfallOracleId = "oracle-test",
                Name = "Test Card"
            };

            var printing = new PrintingCard
            {
                Uuid = "printing-1",
                Oracle = oracle,
                Side = "a"
            };

            Assert.Equal("a", printing.Side);
        }

        [Fact]
        public void OracleCardFactory_HandlesMissingOptionalProperties()
        {
            var row = new OracleCardDbRow(
                ScryfallOracleId: "oracle-test",
                Name: "Test Card",
                GamePlayCard: 1,
                ManaCostRaw: null,
                ManaValue: null,
                Colors: null,
                Keywords: null,
                RulesText: null,
                SuperTypes: null,
                Types: null,
                SubTypes: null,
                Type: null);

            var card = OracleCardFactory.FromRow(row);

            Assert.Equal("Test Card", card.Name);
            Assert.Equal(0, card.ManaValue);
            Assert.True(string.IsNullOrEmpty(card.Colors));
            Assert.True(string.IsNullOrEmpty(card.Keywords));
            Assert.True(string.IsNullOrEmpty(card.Types));
            Assert.True(string.IsNullOrEmpty(card.Text));
        }
    }
}
