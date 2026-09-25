using CollectaMundo.ApplicationServices.Decks.DeckExport;
using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.DomainLogic.Decks.Models.Enums;
using CollectaMundo.DomainLogic.Shared.CardModels;

namespace CollectaMundo.Tests.UnitTests
{
    public class DeckExportFormatterTests
    {
        [Fact]
        public void FormatCardmarketWantList_UsesMissingQuantityAndSortsByCardName()
        {
            // Arrange
            var cards = new[]
            {
                new MissingDeckCard
                {
                    Card = CreateOracleCard("oracle-sol-ring","Sol Ring"),
                    DesiredQuantity = 1,
                    AvailableQuantity = 0,
                    MissingQuantity = 1
                },
                new MissingDeckCard
                {
                    Card = CreateOracleCard("oracle-counterspell","Counterspell"),
                    DesiredQuantity = 4,
                    AvailableQuantity = 1,
                    MissingQuantity = 3
                },
                new MissingDeckCard
                {
                    Card = CreateOracleCard("oracle-lightning-bolt","Lightning Bolt"),
                    DesiredQuantity = 4,
                    AvailableQuantity = 2,
                    MissingQuantity = 2
                }
            };

            var expected = string.Join(Environment.NewLine, "3 Counterspell", "2 Lightning Bolt", "1 Sol Ring");

            // Act
            var result = DeckExportFormatter.FormatCardmarketWantList(cards);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void GetCompleteDeckCsvHeaders_ReturnsExpectedSchema()
        {
            // Act
            var result = DeckExportFormatter.GetCompleteDeckCsvHeaders();

            // Assert
            Assert.Equal(
            [
                "Quantity",
                "Name",
                "Section",
                "Type",
                "Mana Cost",
                "Scryfall Oracle ID"
            ],
            result);
        }

        [Fact]
        public void CreateCompleteDeckCsvRows_ProjectsExpectedFieldsAndPreservesOrder()
        {
            // Arrange
            var commander = CreateOracleCard("oracle-commander", "Test Commander", type: "Legendary Creature — Human Wizard", manaCost: "{2}{U}");
            var lightningBolt = CreateOracleCard("oracle-lightning-bolt", "Lightning Bolt", type: "Instant", manaCost: "{R}");

            var cards = new[]
            {
                new DeckCardState
                {
                    Card = commander,
                    DesiredQuantity = 1,
                    Section = DeckSection.Commander
                },
                new DeckCardState
                {
                    Card = lightningBolt,
                    DesiredQuantity = 4,
                    Section = DeckSection.Mainboard
                }
            };

            // Act
            var result = DeckExportFormatter.CreateCompleteDeckCsvRows(cards).ToList();

            // Assert
            Assert.Equal(2, result.Count);

            Assert.Equal(
            [
                "1",
                "Test Commander",
                "Commander",
                "Legendary Creature — Human Wizard",
                "{2}{U}",
                "oracle-commander"
            ],
            result[0]);

            Assert.Equal(
            [
                "4",
                "Lightning Bolt",
                "Mainboard",
                "Instant",
                "{R}",
                "oracle-lightning-bolt"
            ],
            result[1]);
        }

        // Helper method to create an OracleCard instance for testing

        private static OracleCard CreateOracleCard(string oracleId, string name, string? type = null, string? manaCost = null)
        {
            return new OracleCard
            {
                ScryfallOracleId = oracleId,
                Name = name,
                Type = type,
                ManaCost = manaCost
            };
        }
    }
}
