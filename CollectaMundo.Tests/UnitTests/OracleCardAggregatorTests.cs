using CollectaMundo.DomainLogic.CardData;
using CollectaMundo.DomainLogic.CardData.Models;

namespace CollectaMundo.Tests.UnitTests
{
    public sealed class OracleCardAggregatorTests
    {
        [Fact]
        public void SingleFace_PreservesProperties()
        {
            var face = CreateFace(
                oracleId: "oracle-1",
                side: "",
                name: "Lightning Bolt",
                manaCost: "{R}",
                manaValue: 1,
                types: "Instant",
                keywords: "");

            var result = OracleCardAggregator.Aggregate([face]);

            var card = Assert.Single(result);

            Assert.Equal("oracle-1", card.ScryfallOracleId);
            Assert.Equal("Lightning Bolt", card.Name);
            Assert.Equal("{R}", card.ManaCostRaw);
            Assert.Equal(1, card.ManaValue);
            Assert.Equal("Instant", card.Types);
        }

        [Fact]
        public void MultipleFaces_UsesFrontManaValues_AndUnionsTypes()
        {
            const string name =
                "Blex, Vexing Pest // Search for Blex";

            var front = CreateFace(
                "oracle-1", "a", name,
                "{2}{G}", 3,
                types: "Creature",
                superTypes: "Legendary",
                subTypes: "Pest",
                type: "Legendary Creature — Pest");

            var back = CreateFace(
                "oracle-1", "b", name,
                "{2}{B}{B}", 4,
                types: "Sorcery",
                type: "Sorcery");

            // Reverse input order intentionally.
            var card = Assert.Single(
                OracleCardAggregator.Aggregate([back, front]));

            Assert.Equal("{2}{G}", card.ManaCostRaw);
            Assert.Equal(3, card.ManaValue);
            Assert.Equal("Legendary Creature — Pest", card.Type);
            Assert.Equal("Creature,Sorcery", card.Types);
            Assert.Equal("Legendary", card.SuperTypes);
            Assert.Equal("Pest", card.SubTypes);
        }

        [Fact]
        public void MultipleFaces_UnionsSearchableProperties()
        {
            var front = CreateFace(
                "oracle-1", "a", "Test // Test",
                keywords: "Flying, Haste",
                colors: "G",
                types: "Creature");

            var back = CreateFace(
                "oracle-1", "b", "Test // Test",
                keywords: "Haste, Vigilance",
                colors: "U",
                types: "Enchantment");

            var card = Assert.Single(
                OracleCardAggregator.Aggregate([front, back]));

            Assert.Equal(
                "Flying,Haste,Vigilance", card.Keywords);
            Assert.Equal("G,U", card.Colors);
            Assert.Equal(
                "Creature,Enchantment", card.Types);
        }

        [Fact]
        public void Aggregation_IsIndependentOfInputOrder()
        {
            var a = CreateFace(
                "oracle-1", "a", "Test // Test",
                keywords: "Flying");

            var b = CreateFace(
                "oracle-1", "b", "Test // Test",
                keywords: "Haste");

            var first = OracleCardAggregator.Aggregate([a, b]);
            var second = OracleCardAggregator.Aggregate([b, a]);

            Assert.Equal(first, second);
        }

        [Fact]
        public void ConflictingNames_Throws()
        {
            var a = CreateFace(
                "oracle-1", "a", "Card A");

            var b = CreateFace(
                "oracle-1", "b", "Card B");

            Assert.Throws<InvalidOperationException>(() =>
                OracleCardAggregator.Aggregate([a, b]));
        }

        private static CanonicalOracleFace CreateFace(
            string oracleId,
            string side,
            string name,
            string? manaCost = null,
            double? manaValue = null,
            string? types = null,
            string? keywords = null,
            string? colors = null,
            string? superTypes = null,
            string? subTypes = null,
            string? type = null)
        {
            return new CanonicalOracleFace(
                Key: new OracleFaceKey(oracleId, side),
                Payload: new OracleFacePayload(
                    Name: name,
                    ManaCostRaw: manaCost,
                    ManaValue: manaValue,
                    Colors: colors,
                    Keywords: keywords,
                    RulesText: null,
                    SuperTypes: superTypes,
                    Types: types,
                    SubTypes: subTypes,
                    Type: type),
                GamePlayCard: 1,
                SelectedSourceUuid: Guid.NewGuid().ToString(),
                SourceRowCount: 1,
                WinningRowCount: 1,
                VariantCount: 1,
                IsAmbiguous: false);
        }
    }
}

