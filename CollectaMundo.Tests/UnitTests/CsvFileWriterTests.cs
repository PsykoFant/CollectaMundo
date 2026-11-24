using CollectaMundo.DomainLogic.Decks;
using CollectaMundo.DomainLogic.Decks.Models;
using CollectaMundo.DomainLogic.Decks.Models.Enums;
using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.DomainLogic.Shared.CollectionSnapshot;
using CollectaMundo.Infrastructure.Shared.Files;
using System.IO;
using System.Text;

namespace CollectaMundo.Tests.UnitTests
{
    public class CsvFileWriterTests
    {
        [Fact]
        public async Task WriteAsync_WritesHeaders()
        {
            // Arrange
            using var output = new MemoryStream();
            var writer = new CsvFileWriter(_ => output);

            // Act
            await writer.WriteAsync("collection.csv", ["Name", "Quantity", "Location"], EmptyRows(), ';');

            // Assert
            Assert.Equal($"Name;Quantity;Location{Environment.NewLine}", ReadCsvText(output));
        }

        [Fact]
        public async Task WriteAsync_WritesMultipleRows()
        {
            using var output = new MemoryStream();
            var writer = new CsvFileWriter(_ => output);

            var rows = CreateRows(
                ["Lightning Bolt", "4"],
                ["Counterspell", "2"],
                ["Sol Ring", "1"]);

            await writer.WriteAsync("collection.csv", ["Name", "Quantity"], rows, ';');

            var expected = string.Join(Environment.NewLine,
                "Name;Quantity",
                "Lightning Bolt;4",
                "Counterspell;2",
                "Sol Ring;1",
                string.Empty);

            Assert.Equal(expected, ReadCsvText(output));
        }

        [Fact]
        public async Task WriteAsync_UsesRequestedDelimiter()
        {
            // Arrange
            using var output = new MemoryStream();
            var writer = new CsvFileWriter(_ => output);

            var rows = CreateRows(["Lightning Bolt", "4"]);

            // Act
            await writer.WriteAsync("collection.csv", ["Name", "Quantity"], rows, ',');

            // Assert
            var expected = string.Join(Environment.NewLine, "Name,Quantity", "Lightning Bolt,4", string.Empty);

            Assert.Equal(expected, ReadCsvText(output));
        }

        [Fact]
        public async Task WriteAsync_WritesNullAndEmptyValuesAsEmptyFields()
        {
            // Arrange
            using var output = new MemoryStream();
            var writer = new CsvFileWriter(_ => output);

            var headers = new[]
                {
                    "Name",
                    "Description",
                    "Location"
                };

            var rows = CreateRows(["Lightning Bolt", null, ""]);

            // Act
            await writer.WriteAsync("collection.csv", headers, rows, ';');

            // Assert
            var expected = string.Join(Environment.NewLine,
                "Name;Description;Location",
                "Lightning Bolt;;",
                string.Empty);

            Assert.Equal(expected, ReadCsvText(output));

            Assert.Equal(expected, ReadCsvText(output));
        }

        [Fact]
        public async Task WriteAsync_QuotesValueContainingDelimiter()
        {
            // Arrange
            using var output = new MemoryStream();
            var writer = new CsvFileWriter(_ => output);

            var headers = new[] { "Name", "Description" };
            var rows = CreateRows(["Lightning Bolt", "Fast; efficient"]);

            // Act
            await writer.WriteAsync("collection.csv", headers, rows, ';');

            // Assert
            var expected = string.Join(Environment.NewLine,
                "Name;Description",
                "Lightning Bolt;\"Fast; efficient\"",
                string.Empty);

            Assert.Equal(expected, ReadCsvText(output));
        }

        [Fact]
        public async Task WriteAsync_PreservesUnicodeCharacters()
        {
            // Arrange
            using var output = new MemoryStream();
            var writer = new CsvFileWriter(_ => output);

            var headers = new[]
                {
                    "Name",
                    "Description"
                };

            var rows = CreateRows(["Æther Vial", "Dansk tekst: æøå"]);

            // Act
            await writer.WriteAsync("collection.csv", headers, rows, ';');

            // Assert
            var text = ReadCsvText(output);

            Assert.Contains("Æther Vial", text);
            Assert.Contains("æøå", text);
        }

        [Fact]
        public async Task WriteAsync_ThrowsOperationCanceledException_WhenCancelledBeforeStart()
        {
            // Arrange
            using var output = new MemoryStream();
            var writer = new CsvFileWriter(_ => output);

            using var cts = new CancellationTokenSource();
            cts.Cancel();


            // Act / Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => writer.WriteAsync(
                    "collection.csv",
                    ["Name"],
                    CreateRows(["Lightning Bolt"]),
                    ';',
                    cts.Token));

        }
        [Fact]
        public void GetCompleteDeck_IncludesExportableSectionsAndExcludesMaybeboard()
        {
            // Arrange
            var mainboard = CreateCard("oracle-main", "Main Card");
            var sideboard = CreateCard("oracle-side", "Side Card");
            var commander = CreateCard("oracle-commander", "Commander Card");
            var companion = CreateCard("oracle-companion", "Companion Card");
            var maybeboard = CreateCard("oracle-maybe", "Maybe Card");

            var cards = new[]
            {
                CreateDeckCard(mainboard, 4, DeckSection.Mainboard),
                CreateDeckCard(sideboard, 2, DeckSection.Sideboard),
                CreateDeckCard(commander, 1, DeckSection.Commander),
                CreateDeckCard(companion, 1, DeckSection.Companion),
                CreateDeckCard(maybeboard, 3, DeckSection.Maybeboard)
            };

            // Act
            var deckExportLogic = new DeckExportLogic();
            var result = deckExportLogic.GetCompleteDeck(cards);

            // Assert
            Assert.Equal(4, result.Count);

            Assert.Contains(result, x =>
                x.Card.ScryfallOracleId == mainboard.ScryfallOracleId &&
                x.DesiredQuantity == 4 &&
                x.Section == DeckSection.Mainboard);

            Assert.Contains(result, x =>
                x.Card.ScryfallOracleId == sideboard.ScryfallOracleId &&
                x.DesiredQuantity == 2 &&
                x.Section == DeckSection.Sideboard);

            Assert.Contains(result, x =>
                x.Card.ScryfallOracleId == commander.ScryfallOracleId &&
                x.DesiredQuantity == 1 &&
                x.Section == DeckSection.Commander);

            Assert.Contains(result, x =>
                x.Card.ScryfallOracleId == companion.ScryfallOracleId &&
                x.DesiredQuantity == 1 &&
                x.Section == DeckSection.Companion);

            Assert.DoesNotContain(result, x =>
                x.Card.ScryfallOracleId == maybeboard.ScryfallOracleId);
        }

        [Fact]
        public void GetCompleteDeck_PreservesSameOracleCardAcrossDifferentSections()
        {
            // Arrange
            var card = CreateCard("oracle-bolt", "Lightning Bolt");

            var cards = new[]
            {
                CreateDeckCard(card, 3, DeckSection.Mainboard),
                CreateDeckCard(card, 1, DeckSection.Sideboard)
            };

            // Act
            var deckExportLogic = new DeckExportLogic();
            var result = deckExportLogic.GetCompleteDeck(cards);

            // Assert
            Assert.Equal(2, result.Count);

            Assert.Contains(result, x =>
                x.Section == DeckSection.Mainboard &&
                x.DesiredQuantity == 3);

            Assert.Contains(result, x =>
                x.Section == DeckSection.Sideboard &&
                x.DesiredQuantity == 1);
        }

        [Fact]
        public void GetMissingCards_AggregatesSameOracleCardAcrossSections()
        {
            // Arrange
            const int deckLocationId = 42;

            var card = CreateCard("oracle-bolt", "Lightning Bolt");

            var cards = new[]
            {
                CreateDeckCard(card, 4, DeckSection.Mainboard),
                CreateDeckCard(card, 2, DeckSection.Sideboard)
            };

            var snapshot = new FakeCollectionQuantitySnapshot();
            snapshot.SetAvailableQuantity(card.ScryfallOracleId, deckLocationId, 4);

            // Act
            var deckExportLogic = new DeckExportLogic();
            var result = deckExportLogic.GetMissingCards(cards, snapshot, deckLocationId);

            // Assert
            var missing = Assert.Single(result);

            Assert.Equal(card.ScryfallOracleId, missing.Card.ScryfallOracleId);
            Assert.Equal(6, missing.DesiredQuantity);
            Assert.Equal(4, missing.AvailableQuantity);
            Assert.Equal(2, missing.MissingQuantity);
        }

        [Fact]
        public void GetMissingCards_ReturnsOnlyCardsWithPositiveShortage()
        {
            // Arrange
            const int deckLocationId = 42;

            var partiallyMissing = CreateCard("oracle-a", "Card A");
            var fullyAvailable = CreateCard("oracle-b", "Card B");
            var overAvailable = CreateCard("oracle-c", "Card C");

            var cards = new[]
            {
                CreateDeckCard(partiallyMissing, 4, DeckSection.Mainboard),
                CreateDeckCard(fullyAvailable, 2, DeckSection.Mainboard),
                CreateDeckCard(overAvailable, 1, DeckSection.Sideboard)
            };

            var snapshot = new FakeCollectionQuantitySnapshot();

            snapshot.SetAvailableQuantity(partiallyMissing.ScryfallOracleId, deckLocationId, 2);

            snapshot.SetAvailableQuantity(fullyAvailable.ScryfallOracleId, deckLocationId, 2);

            snapshot.SetAvailableQuantity(overAvailable.ScryfallOracleId, deckLocationId, 5);

            // Act
            var deckExportLogic = new DeckExportLogic();
            var result = deckExportLogic.GetMissingCards(cards, snapshot, deckLocationId);

            // Assert
            var missing = Assert.Single(result);

            Assert.Equal(partiallyMissing.ScryfallOracleId, missing.Card.ScryfallOracleId);

            Assert.Equal(4, missing.DesiredQuantity);
            Assert.Equal(2, missing.AvailableQuantity);
            Assert.Equal(2, missing.MissingQuantity);
        }

        // Helper methods for creating test data
        private static DeckCardState CreateDeckCard(OracleCard card, int quantity, DeckSection section)
        {
            return new DeckCardState
            {
                Card = card,
                DesiredQuantity = quantity,
                Section = section
            };
        }
        private static OracleCard CreateCard(string oracleId, string name)
        {
            return new OracleCard
            {
                ScryfallOracleId = oracleId,
                Name = name
            };
        }
        private sealed class FakeCollectionQuantitySnapshot : ICollectionQuantitySnapshot
        {
            private readonly Dictionary<(string OracleId, int LocationId), int> _availableQuantities = [];

            public void SetAvailableQuantity(string oracleId, int locationId, int quantity)
            {
                _availableQuantities[(oracleId, locationId)] = quantity;
            }

            public int GetAvailableQuantity(string oracleId, int locationId)
            {
                return _availableQuantities.GetValueOrDefault((oracleId, locationId));
            }

            public int GetOwnedQuantity(string oracleId)
            {
                throw new NotSupportedException();
            }

            public int GetAllocatedQuantity(string oracleId, int locationId)
            {
                throw new NotSupportedException();
            }
        }
        private static string CreateTempFilePath()
        {
            return Path.Combine(
                Path.GetTempPath(),
                $"{Guid.NewGuid():N}.csv");
        }
        private static void DeleteFileIfExists(string filePath)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        private static async IAsyncEnumerable<IReadOnlyList<string?>> EmptyRows()
        {
            await Task.CompletedTask;
            yield break;
        }
        private static async IAsyncEnumerable<IReadOnlyList<string?>> CreateRows(params string?[][] rows)
        {
            foreach (var row in rows)
            {
                yield return row;
                await Task.Yield();
            }
        }
        private static string ReadCsvText(MemoryStream output)
        {
            using var input = new MemoryStream(output.ToArray());
            using var reader = new StreamReader(input, Encoding.UTF8);
            return reader.ReadToEnd();
        }
    }
}
