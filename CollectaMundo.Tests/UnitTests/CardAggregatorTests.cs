using CollectaMundo.DomainLogic.CardLists;
using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.Tests.TestUtils;

namespace CollectaMundo.Tests.UnitTests
{
    public class CardAggregatorTests
    {
        [Fact]
        public void Aggregates_SingleCard_NoMergeRequired()
        {
            var input = new List<PrintingCard>
            {
                TestCardFactory.CreatePrinting(uuid: "card1",keywords: "Flying",colors: "W",types: "Creature",text: "Some ability",side: "a")
            };

            var result = PrintingCardAggregator.Aggregate(input);

            Assert.Single(result);

            var card = result[0];

            Assert.Equal("Flying", card.Keywords);
            Assert.Equal("W", card.Colors);
            Assert.Equal("Creature", card.Types);
            Assert.Equal("Some ability", card.Text);
        }

        [Fact]
        public void Aggregates_MultiFaceCards_MergesCorrectly()
        {
            var input = new List<PrintingCard>
            {
                TestCardFactory.CreatePrinting(uuid: "front",keywords: "Flying, Haste",colors: "W, R",types: "Creature",text: "Front text",side: "a",otherFaceIds: ["back"]),
                TestCardFactory.CreatePrinting(uuid: "back",keywords: "Haste, Trample",colors: "R, G",types: "Artifact",text: "Back text",side: "b",otherFaceIds: ["front"])
            };

            var result = PrintingCardAggregator.Aggregate(input);

            Assert.Single(result);
            var card = result[0];
            Assert.Equal("W,R,G", card.Colors); // deduplicated & joined
            Assert.Equal("Flying,Haste,Trample", card.Keywords); // deduplicated
            Assert.Contains("Creature", card.Types);
            Assert.Contains("Artifact", card.Types);
            Assert.Equal("Front text // Back text", card.Text);
        }

        [Fact]
        public void Ignores_NonPrimaryFaces_InOutput()
        {
            var input = new List<PrintingCard>
            {
                TestCardFactory.CreatePrinting(name: "Back Only", uuid: "back", side: "b")
            };

            var result = PrintingCardAggregator.Aggregate(input);

            Assert.Empty(result);
        }

        [Fact]
        public void Deduplication_Is_CaseInsensitive()
        {
            var input = new List<PrintingCard>
                {
                    TestCardFactory.CreatePrinting(name: "Test Card", uuid: "card1", side: "a", keywords: "Flying, flying, FLYING", colors: "W, w, W", types: "Creature, creature")
                };

            var result = PrintingCardAggregator.Aggregate(input);
            Assert.Single(result);

            var card = result[0];
            Assert.Equal("Flying", card.Keywords);
            Assert.Equal("W", card.Colors);
            Assert.Equal("Creature", card.Types);
        }

        [Fact]
        public void Handles_MissingTextOrKeywordsOrColors_Gracefully()
        {
            var input = new List<PrintingCard>
                {
                    TestCardFactory.CreatePrinting(name: "Test Card1", uuid: "card1", side: "a"),
                    TestCardFactory.CreatePrinting(name: "Test Card2", uuid: "card2", side: "a")
                };

            var result = PrintingCardAggregator.Aggregate(input);
            Assert.Equal(2, result.Count);
        }
    }
}
