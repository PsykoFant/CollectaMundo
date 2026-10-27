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

            var relationships = CreateRelationships("oracle-1");

            var result = OracleCardAggregator.Aggregate(
                [face], relationships);

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

            var relationships = CreateRelationships("oracle-1");

            // Reverse input order intentionally.
            var card = Assert.Single(
                OracleCardAggregator.Aggregate(
                    [back, front], relationships));

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

            var relationships = CreateRelationships("oracle-1");

            var card = Assert.Single(
                OracleCardAggregator.Aggregate(
                    [front, back], relationships));

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

            var relationships = CreateRelationships("oracle-1");

            var first = OracleCardAggregator.Aggregate(
                [a, b], relationships);

            var second = OracleCardAggregator.Aggregate(
                [b, a], relationships);

            Assert.Equal(first, second);
        }

        [Fact]
        public void DifferentFaceNames_UsesPrimaryFaceName()
        {
            const string oracleId =
                "078def07-ae5d-4591-8db6-d156834aab97";

            var front = CreateFace(
                oracleId,
                "a",
                "Etali, Primal Storm // Etali, Primal Storm",
                types: "Creature");

            var back = CreateFace(
                oracleId,
                "b",
                "Etali, Primal Storm",
                types: "Creature");

            var relationships = CreateRelationships(oracleId);

            // Reverse input order to verify deterministic primary selection.
            var result = OracleCardAggregator.Aggregate(
                [back, front],
                relationships);

            var card = Assert.Single(result);

            Assert.Equal(oracleId, card.ScryfallOracleId);

            Assert.Equal(
                "Etali, Primal Storm // Etali, Primal Storm",
                card.Name);

            Assert.Equal("Creature", card.Types);
        }

        [Fact]
        public void MissingFaceName_Throws()
        {
            var front = CreateFace(
                "oracle-1", "a", "Valid Name");

            var back = CreateFace(
                "oracle-1", "b", "");

            var relationships = CreateRelationships("oracle-1");

            Assert.Throws<InvalidOperationException>(() =>
                OracleCardAggregator.Aggregate(
                    [front, back],
                    relationships));
        }

        // =========================================================
        // Cross-Oracle supporting-face regression tests
        // =========================================================

        [Fact]
        public void MeldFace_ContributesKeywordsToBothPrimaryOracleCards()
        {
            var bruna = CreateFace(
                oracleId: "oracle-bruna",
                side: "a",
                name: "Bruna, the Fading Light // Brisela, Voice of Nightmares",
                manaCost: "{5}{W}{W}",
                manaValue: 7,
                keywords: "Flying,Vigilance",
                types: "Creature",
                superTypes: "Legendary",
                subTypes: "Angel",
                type: "Legendary Creature — Angel");

            var gisela = CreateFace(
                oracleId: "oracle-gisela",
                side: "a",
                name: "Gisela, the Broken Blade // Brisela, Voice of Nightmares",
                manaCost: "{2}{W}{W}",
                manaValue: 4,
                keywords: "Flying,Lifelink,First strike,Meld",
                types: "Creature",
                superTypes: "Legendary",
                subTypes: "Angel",
                type: "Legendary Creature — Angel");

            var brisela = CreateFace(
                oracleId: "oracle-brisela",
                side: "b",
                name: "Brisela, Voice of Nightmares",
                keywords: "Flying,Lifelink,Vigilance,First strike",
                types: "Creature",
                superTypes: "Legendary",
                subTypes: "Eldrazi,Angel",
                type: "Legendary Creature — Eldrazi Angel");

            var relationships = new OracleFaceRelationshipResolution(
                StandaloneOracleIds:
                [
                    "oracle-bruna",
                    "oracle-gisela"
                ],
                SupportingFaces:
                [
                    new OracleCardFaceLink(
                        "oracle-bruna",
                        new OracleFaceKey("oracle-brisela", "b")),

                    new OracleCardFaceLink(
                        "oracle-gisela",
                        new OracleFaceKey("oracle-brisela", "b"))
                ],
                MissingReferences: []);

            var result = OracleCardAggregator.Aggregate(
                [bruna, gisela, brisela],
                relationships);

            // Brisela must not be a standalone Oracle card.
            Assert.Equal(2, result.Count);

            Assert.DoesNotContain(result,
                card => card.ScryfallOracleId == "oracle-brisela");

            var aggregatedBruna = Assert.Single(result,
                card => card.ScryfallOracleId == "oracle-bruna");

            var aggregatedGisela = Assert.Single(result,
                card => card.ScryfallOracleId == "oracle-gisela");

            // Both primary cards inherit Brisela's keywords.
            Assert.Contains("Vigilance",
                aggregatedBruna.Keywords!.Split(','));

            Assert.Contains("Vigilance",
                aggregatedGisela.Keywords!.Split(','));

            // Gisela must retain its own Meld keyword.
            Assert.Contains("Meld",
                aggregatedGisela.Keywords!.Split(','));

            // Supporting faces contribute searchable types.
            Assert.Equal("Creature", aggregatedBruna.Types);
            Assert.Equal("Creature", aggregatedGisela.Types);
        }

        [Fact]
        public void MeldFace_PreservesPrimaryScalarProperties()
        {
            var primary = CreateFace(
                oracleId: "oracle-gisela",
                side: "a",
                name: "Gisela // Brisela",
                manaCost: "{2}{W}{W}",
                manaValue: 4,
                superTypes: "Legendary",
                subTypes: "Angel",
                type: "Legendary Creature — Angel");

            var supporting = CreateFace(
                oracleId: "oracle-brisela",
                side: "b",
                name: "Brisela",
                manaCost: null,
                manaValue: 0,
                keywords: "Vigilance",
                superTypes: "Legendary",
                subTypes: "Eldrazi,Angel",
                type: "Legendary Creature — Eldrazi Angel");

            var relationships = new OracleFaceRelationshipResolution(
                StandaloneOracleIds: ["oracle-gisela"],
                SupportingFaces:
                [
                    new OracleCardFaceLink(
                        "oracle-gisela",
                        new OracleFaceKey("oracle-brisela", "b"))
                ],
                MissingReferences: []);

            var card = Assert.Single(
                OracleCardAggregator.Aggregate(
                    [primary, supporting],
                    relationships));

            Assert.Equal("Gisela // Brisela", card.Name);
            Assert.Equal("{2}{W}{W}", card.ManaCostRaw);
            Assert.Equal(4, card.ManaValue);
            Assert.Equal("Legendary Creature — Angel", card.Type);

            // Cross-Oracle faces must not change these properties.
            Assert.Equal("Legendary", card.SuperTypes);
            Assert.Equal("Angel", card.SubTypes);

            // But they must contribute searchable properties.
            Assert.Equal("Vigilance", card.Keywords);
        }

        [Fact]
        public void DuplicateSupportingFaceLinks_DoNotDuplicateProperties()
        {
            var primary = CreateFace(
                "oracle-primary", "a", "Primary",
                keywords: "Flying");

            var supporting = CreateFace(
                "oracle-support", "b", "Supporting",
                keywords: "Vigilance",
                types: "Creature");

            var link = new OracleCardFaceLink(
                "oracle-primary",
                new OracleFaceKey("oracle-support", "b"));

            var relationships = new OracleFaceRelationshipResolution(
                StandaloneOracleIds: ["oracle-primary"],
                SupportingFaces: [link, link],
                MissingReferences: []);

            var card = Assert.Single(
                OracleCardAggregator.Aggregate(
                    [primary, supporting],
                    relationships));

            Assert.Equal("Flying,Vigilance", card.Keywords);
            Assert.Equal("Creature", card.Types);
        }

        [Fact]
        public void MissingCanonicalSupportingFace_Throws()
        {
            var primary = CreateFace(
                "oracle-primary", "a", "Primary");

            var relationships = new OracleFaceRelationshipResolution(
                StandaloneOracleIds: ["oracle-primary"],
                SupportingFaces:
                [
                    new OracleCardFaceLink(
                        "oracle-primary",
                        new OracleFaceKey("oracle-missing", "b"))
                ],
                MissingReferences: []);

            Assert.Throws<InvalidOperationException>(() =>
                OracleCardAggregator.Aggregate(
                    [primary], relationships));
        }

        // =========================================================
        // Test helpers
        // =========================================================

        private static OracleFaceRelationshipResolution CreateRelationships(
            params string[] standaloneOracleIds)
        {
            return new OracleFaceRelationshipResolution(
                StandaloneOracleIds: standaloneOracleIds,
                SupportingFaces: [],
                MissingReferences: []);
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



