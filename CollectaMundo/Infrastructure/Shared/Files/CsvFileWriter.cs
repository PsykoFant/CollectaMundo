using CollectaMundo.ApplicationServices.Shared.Files;
using System.IO;
using System.Text;

namespace CollectaMundo.Infrastructure.Shared.Files
{
    public sealed class CsvFileWriter : ICsvFileWriter
    {
        public async Task WriteAsync(string filePath, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string?>> rows, char delimiter, CancellationToken cancellationToken = default)
        {
            await using var writer = new StreamWriter(filePath, false, Encoding.UTF8);

            await WriteLineAsync(writer, headers, delimiter, cancellationToken);

            foreach (var row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await WriteLineAsync(writer, row, delimiter, cancellationToken);
            }
        }
        public async Task WriteAsync(string filePath, IReadOnlyList<string> headers, IAsyncEnumerable<IReadOnlyList<string?>> rows, char delimiter, CancellationToken cancellationToken = default)
        {
            await using var writer = new StreamWriter(filePath, false, Encoding.UTF8);

            await WriteLineAsync(writer, headers, delimiter, cancellationToken);

            await foreach (var row in rows.WithCancellation(cancellationToken))
            {
                await WriteLineAsync(writer, row, delimiter, cancellationToken);
            }
        }
        private static async Task WriteLineAsync(StreamWriter writer, IEnumerable<string?> values, char delimiter, CancellationToken cancellationToken)
        {
            var line = string.Join(delimiter, values.Select(value => EscapeField(value, delimiter)));

            await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
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
