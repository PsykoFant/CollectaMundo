using CollectaMundo.ApplicationServices.Shared.Files;
using System.IO;
using System.Text;

namespace CollectaMundo.Infrastructure.Shared.Files
{
    public sealed class CsvFileWriter : ICsvFileWriter
    {
        public async Task WriteAsync(string filePath, IReadOnlyList<string> headers, IAsyncEnumerable<IReadOnlyList<string?>> rows, char delimiter, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            ArgumentNullException.ThrowIfNull(headers);
            ArgumentNullException.ThrowIfNull(rows);

            var directoryPath = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            await using var writer = new StreamWriter(filePath, append: false, encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            await WriteRowAsync(writer, headers, delimiter, cancellationToken);

            await foreach (var row in rows.WithCancellation(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                await WriteRowAsync(writer, row, delimiter, cancellationToken);
            }
        }
        private static async Task WriteRowAsync(StreamWriter writer, IEnumerable<string?> values, char delimiter, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = string.Join(delimiter, values.Select(value => EscapeField(value, delimiter)));

            await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
        }
        private static string EscapeField(string? value, char delimiter)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            bool requiresQuotes =
                value.Contains(delimiter) ||
                value.Contains('"') ||
                value.Contains('\r') ||
                value.Contains('\n');

            if (!requiresQuotes)
            {
                return value;
            }

            var escaped = value.Replace("\"", "\"\"");

            return $"\"{escaped}\"";
        }
    }
}
