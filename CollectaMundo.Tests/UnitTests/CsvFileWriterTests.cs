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
            var writer = new CsvFileWriter();
            var filePath = CreateTempFilePath();

            try
            {
                var headers = new[]
                {
                    "Name",
                    "Quantity",
                    "Location"
                };

                // Act
                await writer.WriteAsync(
                    filePath,
                    headers,
                    EmptyRows(),
                    ';');

                // Assert
                var lines = await File.ReadAllLinesAsync(filePath);

                Assert.Single(lines);
                Assert.Equal("Name;Quantity;Location", lines[0]);
            }
            finally
            {
                DeleteFileIfExists(filePath);
            }
        }

        [Fact]
        public async Task WriteAsync_WritesMultipleRows()
        {
            // Arrange
            var writer = new CsvFileWriter();
            var filePath = CreateTempFilePath();

            try
            {
                var headers = new[]
                {
                    "Name",
                    "Quantity"
                };

                var rows = CreateRows(
                    ["Lightning Bolt", "4"],
                    ["Counterspell", "2"],
                    ["Sol Ring", "1"]);

                // Act
                await writer.WriteAsync(
                    filePath,
                    headers,
                    rows,
                    ';');

                // Assert
                var lines = await File.ReadAllLinesAsync(filePath);

                Assert.Equal(4, lines.Length);

                Assert.Equal("Name;Quantity", lines[0]);
                Assert.Equal("Lightning Bolt;4", lines[1]);
                Assert.Equal("Counterspell;2", lines[2]);
                Assert.Equal("Sol Ring;1", lines[3]);
            }
            finally
            {
                DeleteFileIfExists(filePath);
            }
        }

        [Fact]
        public async Task WriteAsync_UsesRequestedDelimiter()
        {
            // Arrange
            var writer = new CsvFileWriter();
            var filePath = CreateTempFilePath();

            try
            {
                var headers = new[]
                {
                    "Name",
                    "Quantity"
                };

                var rows = CreateRows(
                    ["Lightning Bolt", "4"]);

                // Act
                await writer.WriteAsync(
                    filePath,
                    headers,
                    rows,
                    ',');

                // Assert
                var lines = await File.ReadAllLinesAsync(filePath);

                Assert.Equal("Name,Quantity", lines[0]);
                Assert.Equal("Lightning Bolt,4", lines[1]);
            }
            finally
            {
                DeleteFileIfExists(filePath);
            }
        }

        [Fact]
        public async Task WriteAsync_WritesNullAndEmptyValuesAsEmptyFields()
        {
            // Arrange
            var writer = new CsvFileWriter();
            var filePath = CreateTempFilePath();

            try
            {
                var headers = new[]
                {
                    "Name",
                    "Description",
                    "Location"
                };

                var rows = CreateRows(
                    ["Lightning Bolt", null, ""]);

                // Act
                await writer.WriteAsync(
                    filePath,
                    headers,
                    rows,
                    ';');

                // Assert
                var lines = await File.ReadAllLinesAsync(filePath);

                Assert.Equal(2, lines.Length);
                Assert.Equal("Lightning Bolt;;", lines[1]);
            }
            finally
            {
                DeleteFileIfExists(filePath);
            }
        }

        [Fact]
        public async Task WriteAsync_ReplacesDelimiterInValues()
        {
            // Arrange
            var writer = new CsvFileWriter();
            var filePath = CreateTempFilePath();

            try
            {
                var headers = new[]
                {
                    "Name",
                    "Description"
                };

                var rows = CreateRows(
                    ["Lightning Bolt", "Fast; efficient"]);

                // Act
                await writer.WriteAsync(
                    filePath,
                    headers,
                    rows,
                    ';');

                // Assert
                var lines = await File.ReadAllLinesAsync(filePath);

                Assert.Equal("Lightning Bolt;Fast, efficient", lines[1]);
            }
            finally
            {
                DeleteFileIfExists(filePath);
            }
        }

        [Fact]
        public async Task WriteAsync_PreservesUnicodeCharacters()
        {
            // Arrange
            var writer = new CsvFileWriter();
            var filePath = CreateTempFilePath();

            try
            {
                var headers = new[]
                {
                    "Name",
                    "Description"
                };

                var rows = CreateRows(
                    ["Æther Vial", "Dansk tekst: æøå"]);

                // Act
                await writer.WriteAsync(
                    filePath,
                    headers,
                    rows,
                    ';');

                // Assert
                var text = await File.ReadAllTextAsync(
                    filePath,
                    Encoding.UTF8);

                Assert.Contains("Æther Vial", text);
                Assert.Contains("æøå", text);
            }
            finally
            {
                DeleteFileIfExists(filePath);
            }
        }

        [Fact]
        public async Task WriteAsync_ThrowsOperationCanceledException_WhenCancelledBeforeStart()
        {
            // Arrange
            var writer = new CsvFileWriter();
            var filePath = CreateTempFilePath();

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            try
            {
                // Act / Assert
                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => writer.WriteAsync(
                        filePath,
                        ["Name"],
                        CreateRows(["Lightning Bolt"]),
                        ';',
                        cts.Token));
            }
            finally
            {
                DeleteFileIfExists(filePath);
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
    }
}
