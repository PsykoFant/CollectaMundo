using CollectaMundo.DomainLogic.CardData;
using CollectaMundo.DomainLogic.CardData.Models;

namespace CollectaMundo.Tests.UnitTests
{
    public class OracleFaceCanonicalizerTests
    {
        public class CanonicalizeTests
        {
            [Fact]
            public void Test_Identical_Payloads_Create_One_Canonical_Face()
            {
                var payload = CreatePayload(name: "Test Card", rulesText: "Flying");
                var candidates = new List<OracleFaceCandidate>
                {
                    CreateCandidate(uuid: "uuid-1", oracleId: "oracle-1", side: "a", payload),
                    CreateCandidate(uuid: "uuid-2", oracleId: "oracle-1", side: "a", payload)
                };

                var result = OracleFaceCanonicalizer.Canonicalize(candidates);

                Assert.Single(result);

                var canonical = result[0];

                Assert.Equal("oracle-1", canonical.Key.ScryfallOracleId);
                Assert.Equal("a", canonical.Key.Side);
                Assert.Equal(payload, canonical.Payload);

                Assert.Equal(2, canonical.SourceRowCount);
                Assert.Equal(2, canonical.WinningRowCount);
                Assert.Equal(1, canonical.VariantCount);

                Assert.False(canonical.HasConflict);
                Assert.False(canonical.IsAmbiguous);
            }

            [Fact]
            public void Test_Most_Common_Payload_Wins()
            {
                var correctPayload = CreatePayload(name: "Bloomvine Regent // Claim Territory", rulesText: "Correct back-face text", manaValue: 5);

                var badPayload = CreatePayload(
                    name:
                        "Bloomvine Regent // Claim Territory // " +
                        "Bloomvine Regent // Claim Territory",
                    rulesText: "Wrong front-face text",
                    manaValue: 3);

                var candidates = new List<OracleFaceCandidate>
                {
                    CreateCandidate("uuid-1", "oracle-1", "b", correctPayload),
                    CreateCandidate("uuid-2", "oracle-1", "b", correctPayload),
                    CreateCandidate("uuid-3", "oracle-1", "b", correctPayload),
                    CreateCandidate("uuid-4", "oracle-1", "b", badPayload)
                };

                var result = OracleFaceCanonicalizer.Canonicalize(candidates);

                Assert.Single(result);

                var canonical = result[0];

                Assert.Equal(correctPayload, canonical.Payload);
                Assert.Equal(4, canonical.SourceRowCount);
                Assert.Equal(3, canonical.WinningRowCount);
                Assert.Equal(2, canonical.VariantCount);

                Assert.True(canonical.HasConflict);
                Assert.False(canonical.IsAmbiguous);
            }

            [Fact]
            public void Test_Different_Sides_Are_Canonicalized_Separately()
            {
                var frontPayload = CreatePayload(
                    name: "Test Card // Test Omen",
                    rulesText: "Front face",
                    manaValue: 5);

                var backPayload = CreatePayload(
                    name: "Test Card // Test Omen",
                    rulesText: "Back face",
                    manaValue: 3);

                var candidates = new List<OracleFaceCandidate>
                {
                    CreateCandidate("uuid-1","oracle-1", "a", frontPayload),
                    CreateCandidate("uuid-2", "oracle-1", "b", backPayload)
                };

                var result = OracleFaceCanonicalizer.Canonicalize(candidates);

                Assert.Equal(2, result.Count);

                var front = result.Single(x => x.Key.Side == "a");
                var back = result.Single(x => x.Key.Side == "b");

                Assert.Equal(frontPayload, front.Payload);
                Assert.Equal(backPayload, back.Payload);
            }

            [Fact]
            public void Test_Tied_Payloads_Select_Deterministically()
            {
                var payloadA = CreatePayload(name: "Payload A");
                var payloadB = CreatePayload(name: "Payload B");

                var candidates = new List<OracleFaceCandidate>
                {
                    CreateCandidate("uuid-z", "oracle-1", "a", payloadA),
                    CreateCandidate("uuid-a", "oracle-1", "a", payloadB)
                };

                var result = OracleFaceCanonicalizer.Canonicalize(candidates);

                Assert.Single(result);

                var canonical = result[0];

                Assert.Equal(payloadB, canonical.Payload);
                Assert.Equal("uuid-a", canonical.SelectedSourceUuid);

                Assert.Equal(2, canonical.SourceRowCount);
                Assert.Equal(1, canonical.WinningRowCount);
                Assert.Equal(2, canonical.VariantCount);

                Assert.True(canonical.HasConflict);
                Assert.True(canonical.IsAmbiguous);
            }

            [Fact]
            public void Test_Null_And_Empty_Side_Are_Same_Identity()
            {
                var payload = CreatePayload(name: "Single-Faced Card");
                var candidates = new List<OracleFaceCandidate>
                {
                    CreateCandidate("uuid-1", "oracle-1", null, payload),
                    CreateCandidate("uuid-2", "oracle-1", string.Empty, payload)
                };

                var result = OracleFaceCanonicalizer.Canonicalize(candidates);

                Assert.Single(result);

                var canonical = result[0];

                Assert.Equal(string.Empty, canonical.Key.Side);
                Assert.Equal(2, canonical.SourceRowCount);
                Assert.Equal(1, canonical.VariantCount);
            }
        }
        private static OracleFaceCandidate CreateCandidate(string uuid, string oracleId, string? side, OracleFacePayload payload)
        {
            return new OracleFaceCandidate(SourceUuid: uuid, ScryfallOracleId: oracleId, Side: side, Payload: payload);
        }
        private static OracleFacePayload CreatePayload(
            string? name = "Test Card",
            string? manaCostRaw = "{2}{G}",
            double? manaValue = 3,
            string? colors = "G",
            string? keywords = "Flying",
            string? rulesText = "Test rules text",
            string? superTypes = null,
            string? types = "Creature",
            string? subTypes = "Dragon",
            string? type = "Creature — Dragon")
        {
            return new OracleFacePayload(
                Name: name,
                ManaCostRaw: manaCostRaw,
                ManaValue: manaValue,
                Colors: colors,
                Keywords: keywords,
                RulesText: rulesText,
                SuperTypes: superTypes,
                Types: types,
                SubTypes: subTypes,
                Type: type);
        }
    }
}
