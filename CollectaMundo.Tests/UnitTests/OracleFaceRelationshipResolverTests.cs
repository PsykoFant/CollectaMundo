using CollectaMundo.DomainLogic.CardData;
using CollectaMundo.DomainLogic.CardData.Models;

namespace CollectaMundo.Tests.UnitTests
{
    public class OracleFaceRelationshipResolverTests
    {
        [Fact]
        public void Resolve_MeldBack_SharedByTwoPrimaryCards()
        {
            var candidates = new[]
            {
                Face("bruna", "oracle-bruna", "a", "brisela"),
                Face("gisela", "oracle-gisela", "a", "brisela"),
                Face("brisela", "oracle-brisela", "b", "bruna, gisela")
            };

            var result = OracleFaceRelationshipResolver.Resolve(candidates);

            Assert.Equal(
                ["oracle-bruna", "oracle-gisela"],
                result.StandaloneOracleIds);

            Assert.Equal(2, result.SupportingFaces.Count);

            Assert.Contains(
                new OracleCardFaceLink(
                    "oracle-bruna",
                    new OracleFaceKey("oracle-brisela", "b")),
                result.SupportingFaces);

            Assert.Contains(
                new OracleCardFaceLink(
                    "oracle-gisela",
                    new OracleFaceKey("oracle-brisela", "b")),
                result.SupportingFaces);

            Assert.Empty(result.MissingReferences);
        }

        [Fact]
        public void Resolve_AftermathFaces_SharingOracleId_CreateNoCrossOracleLinks()
        {
            var candidates = new[]
            {
                Face("never", "oracle-never", "a", "return"),
                Face("return", "oracle-never", "b", "never")
            };

            var result = OracleFaceRelationshipResolver.Resolve(candidates);

            Assert.Equal(
                ["oracle-never"],
                result.StandaloneOracleIds);

            Assert.Empty(result.SupportingFaces);
            Assert.Empty(result.MissingReferences);
        }

        [Fact]
        public void Resolve_RepeatedPrintings_DoNotDuplicateSupportingFaces()
        {
            var candidates = new[]
            {
                Face("bruna-1", "oracle-bruna", "a", "brisela-1"),
                Face("bruna-2", "oracle-bruna", "a", "brisela-2"),

                Face("brisela-1", "oracle-brisela", "b"),
                Face("brisela-2", "oracle-brisela", "b")
            };

            var result = OracleFaceRelationshipResolver.Resolve(candidates);

            var link = Assert.Single(result.SupportingFaces);

            Assert.Equal("oracle-bruna", link.PrimaryOracleId);
            Assert.Equal(
                new OracleFaceKey("oracle-brisela", "b"),
                link.SupportingFace);
        }

        [Fact]
        public void Resolve_MissingRelatedUuid_IsReported()
        {
            var candidates = new[]
            {
                Face("front", "oracle-front", "a", "missing-back")
            };

            var result = OracleFaceRelationshipResolver.Resolve(candidates);

            Assert.Equal(
                ["oracle-front"],
                result.StandaloneOracleIds);

            Assert.Empty(result.SupportingFaces);

            Assert.Equal(
                new MissingOracleFaceReference("front", "missing-back"),
                Assert.Single(result.MissingReferences));
        }

        [Fact]
        public void Resolve_IsIndependentOfInputOrder()
        {
            var candidates = new[]
            {
                Face("bruna", "oracle-bruna", "a", "brisela"),
                Face("gisela", "oracle-gisela", "a", "brisela"),
                Face("brisela", "oracle-brisela", "b")
            };

            var forward = OracleFaceRelationshipResolver.Resolve(candidates);
            var reversed = OracleFaceRelationshipResolver.Resolve(
                candidates.Reverse().ToArray());

            Assert.Equal(
                forward.StandaloneOracleIds,
                reversed.StandaloneOracleIds);

            Assert.Equal(
                forward.SupportingFaces,
                reversed.SupportingFaces);

            Assert.Equal(
                forward.MissingReferences,
                reversed.MissingReferences);
        }

        private static OracleFaceCandidate Face(
            string uuid,
            string oracleId,
            string side,
            string? otherFaceIds = null)
        {
            return new OracleFaceCandidate(
                SourceUuid: uuid,
                ScryfallOracleId: oracleId,
                Side: side,
                GamePlayCard: 1,
                Payload: new OracleFacePayload(
                    Name: oracleId,
                    ManaCostRaw: null,
                    ManaValue: null,
                    Colors: null,
                    Keywords: null,
                    RulesText: null,
                    SuperTypes: null,
                    Types: null,
                    SubTypes: null,
                    Type: null),
                OtherFaceIds: otherFaceIds);
        }
    }
}

