using CollectaMundo.DomainLogic.CardLists.Aggregation;
using CollectaMundo.DomainLogic.CardLists.Models;

namespace CollectaMundo.Tests.UnitTests
{
    public class CardAggregatorTests
    {
        private readonly CardCoreAggregator _aggregator = new();

        [Fact]
        public void Aggregates_SingleCard_NoMergeRequired()
        {
            var input = new List<CardCore>
                {
                    new()
                    {
                        Uuid = "card1",
                        Name = "Test Card",
                        Keywords = "Flying",
                        Colors = "W",
                        Types = "Creature",
                        Text = "Some ability",
                        Side = "a"
                    }
                };

            var result = _aggregator.Aggregate(input);

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
            var input = new List<CardCore>
                {
                    new()
                    {
                        Uuid = "front",
                        Name = "Front Face",
                        Keywords = "Flying, Haste",
                        Colors = "W, R",
                        Types = "Creature",
                        Text = "Front text",
                        Side = "a",
                        OtherFaceIds = ["back"]
                    },
                    new()
                    {
                        Uuid = "back",
                        Name = "Back Face",
                        Keywords = "Haste, Trample",
                        Colors = "R, G",
                        Types = "Artifact",
                        Text = "Back text",
                        Side = "b"
                    }
                };

            var result = _aggregator.Aggregate(input);

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
            var input = new List<CardCore>
                {
                    new()
                    {
                        Uuid = "back",
                        Side = "b",
                        Name = "Back Only"
                    }
                };

            var result = _aggregator.Aggregate(input);

            Assert.Empty(result);
        }

        [Fact]
        public void Deduplication_Is_CaseInsensitive()
        {
            var input = new List<CardCore>
                {
                    new()
                    {
                        Name = "Test Card",
                        Uuid = "card1",
                        Side = "a",
                        Keywords = "Flying, flying, FLYING",
                        Colors = "W, w, W",
                        Types = "Creature, creature"
                    }
                };

            var result = _aggregator.Aggregate(input);
            Assert.Single(result);

            var card = result[0];
            Assert.Equal("Flying", card.Keywords);
            Assert.Equal("W", card.Colors);
            Assert.Equal("Creature", card.Types);
        }

        [Fact]
        public void Handles_MissingTextOrKeywordsOrColors_Gracefully()
        {
            var input = new List<CardCore>
                {
                    new()
                    {
                        Name = "Test Card1",
                        Uuid = "card1",
                        Side = "a",
                        Text = null,
                        Keywords = null,
                        Colors = null
                    },
                    new()
                    {
                        Name = "Test Card2",
                        Uuid = "card2",
                        Side = "a",
                        Text = "",
                        Keywords = "",
                        Colors = ""
                    }
                };

            var result = _aggregator.Aggregate(input);
            Assert.Equal(2, result.Count);
        }
    }
}
