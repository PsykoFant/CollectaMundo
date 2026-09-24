using CollectaMundo.DomainLogic.Decks;
using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.DomainLogic.Decks.Models.Enums;
using CollectaMundo.DomainLogic.Shared.CardModels;

namespace CollectaMundo.Tests.UnitTests
{
    public class DeckCardStateMapperTests
    {
        [Fact]
        public void CreateStates_MapsDeckEntryToOracleCard()
        {
            // Arrange
            var oracleCard = CreateOracleCard("oracle-lightning-bolt", "Lightning Bolt");
            var entries = new[]
            {
                CreateEntry(oracleCard.ScryfallOracleId,"Lightning Bolt",4,DeckSection.Mainboard)
            };

            // Act
            var result = DeckCardStateMapper.CreateStates(entries, [oracleCard]);

            // Assert
            var state = Assert.Single(result);

            Assert.Same(oracleCard, state.Card);
            Assert.Equal(4, state.DesiredQuantity);
            Assert.Equal(DeckSection.Mainboard, state.Section);
        }

        [Fact]
        public void CreateStates_OracleIdLookupIsCaseInsensitive()
        {
            // Arrange
            var oracleCard = CreateOracleCard("ABC-DEF", "Lightning Bolt");
            var entries = new[]
            {
                CreateEntry("abc-def","Lightning Bolt",1,DeckSection.Sideboard)
            };

            // Act
            var result = DeckCardStateMapper.CreateStates(entries, [oracleCard]);

            // Assert
            var state = Assert.Single(result);

            Assert.Same(oracleCard, state.Card);
        }

        [Fact]
        public void CreateStates_UnresolvedOracleCard_IsSkipped()
        {
            // Arrange
            var knownCard = CreateOracleCard("known-id", "Known Card");
            var entries = new[]
            {
                CreateEntry("missing-id","Missing Card",2,DeckSection.Mainboard)
            };

            // Act
            var result = DeckCardStateMapper.CreateStates(entries, [knownCard]);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void CreateStates_PreservesMultipleEntriesForSameOracleCard()
        {
            // Arrange
            var oracleCard = CreateOracleCard("oracle-lightning-bolt", "Lightning Bolt");
            var entries = new[]
            {
                CreateEntry(oracleCard.ScryfallOracleId,"Lightning Bolt",3,DeckSection.Mainboard),
                CreateEntry(oracleCard.ScryfallOracleId,"Lightning Bolt",1,DeckSection.Sideboard)
            };

            // Act
            var result = DeckCardStateMapper.CreateStates(entries, [oracleCard]);

            // Assert
            Assert.Equal(2, result.Count);

            Assert.Contains(result, state =>
                state.Card == oracleCard &&
                state.DesiredQuantity == 3 &&
                state.Section == DeckSection.Mainboard);

            Assert.Contains(result, state =>
                state.Card == oracleCard &&
                state.DesiredQuantity == 1 &&
                state.Section == DeckSection.Sideboard);
        }

        // Helper methods to create test data
        private static DeckCardEntry CreateEntry(string oracleId, string cardName, int desiredQuantity, DeckSection section)
        {
            return new DeckCardEntry
            {
                DeckLocationId = 42,
                OracleId = oracleId,
                CardName = cardName,
                DesiredQuantity = desiredQuantity,
                Section = section
            };
        }
        private static OracleCard CreateOracleCard(string oracleId, string name)
        {
            return new OracleCard
            {
                ScryfallOracleId = oracleId,
                Name = name
            };
        }
    }
}
