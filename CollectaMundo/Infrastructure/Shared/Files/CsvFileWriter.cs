using CollectaMundo.ApplicationServices.Shared.Files;
using System.IO;
using System.Text;

namespace CollectaMundo.Infrastructure.Shared.Files
{
    public delegate Task WriteFileOutputAsync(string filePath, Func<Stream, Task> writeContent, CancellationToken cancellationToken);
    public sealed class CsvFileWriter(WriteFileOutputAsync writeOutput) : ICsvFileWriter
    {
        private readonly WriteFileOutputAsync _writeOutput = writeOutput;
        public Task WriteAsync(string filePath, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string?>> rows, char delimiter, CancellationToken cancellationToken = default)
        {
            return _writeOutput(filePath, async output =>
            {
                await using var writer = new StreamWriter(output, Encoding.UTF8, leaveOpen: true);

                await WriteLineAsync(writer, headers, delimiter, cancellationToken);

                foreach (var row in rows)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await WriteLineAsync(writer, row, delimiter, cancellationToken);
                }
            }, cancellationToken);
        }
        public Task WriteAsync(string filePath, IReadOnlyList<string> headers, IAsyncEnumerable<IReadOnlyList<string?>> rows, char delimiter, CancellationToken cancellationToken = default)
        {
            return _writeOutput(filePath, async output =>
            {
                await using var writer = new StreamWriter(output, Encoding.UTF8, leaveOpen: true);

                await WriteLineAsync(writer, headers, delimiter, cancellationToken);

                await foreach (var row in rows.WithCancellation(cancellationToken))
                {
                    await WriteLineAsync(writer, row, delimiter, cancellationToken);
                }
            }, cancellationToken);
        }
        private static async Task WriteLineAsync(StreamWriter writer, IEnumerable<string?> values, char delimiter, CancellationToken cancellationToken)
        {
            var line = string.Join(delimiter, values.Select(value => EscapeField(value, delimiter)));

            await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
        }
        private static void EnsureParentDirectoryExists(string filePath)
        {
            var directoryPath = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
        }
        private static string EscapeField(string? value, char delimiter)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var requiresQuotes =
                value.Contains(delimiter) ||
                value.Contains('"') ||
                value.Contains('\r') ||
                value.Contains('\n');

            if (!requiresQuotes)
            {
                return value;
            }

            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
    }
}
