using CollectaMundo.Infrastructure.Shared.IO;
using System.IO;
using System.IO.Compression;

namespace CollectaMundo.Infrastructure.Shared.FileTransfers
{
    public sealed class GzipFileDecompressor : IGzipFileDecompressor
    {
        private const int BufferSize = 128 * 1024;
        public async Task DecompressAsync(string gzipPath, string destinationPath, IProgress<FileTransferProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(gzipPath);
            ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

            if (!File.Exists(gzipPath))
            {
                throw new FileNotFoundException("GZip source file was not found.", gzipPath);
            }

            var destinationDirectory = Path.GetDirectoryName(destinationPath);

            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            var partialPath = destinationPath + ".decompressing";

            DeleteIfExists(partialPath);

            try
            {
                long compressedLength;

                await using (var compressedStream = new FileStream(gzipPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    compressedLength = compressedStream.Length;

                    using var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress, leaveOpen: true);

                    await using var outputStream = new FileStream(partialPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);

                    var buffer = new byte[BufferSize];

                    var lastReportedPercent = -1;

                    progress?.Report(new FileTransferProgress(BytesTransferred: 0, TotalBytes: compressedLength));

                    while (true)
                    {
                        var bytesRead = await gzipStream.ReadAsync(buffer.AsMemory(), cancellationToken);

                        if (bytesRead == 0)
                        {
                            break;
                        }

                        await outputStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);

                        var transferProgress = new FileTransferProgress(BytesTransferred: compressedStream.Position, TotalBytes: compressedLength);

                        if (transferProgress.Percent is int percent && percent != lastReportedPercent)
                        {
                            lastReportedPercent = percent;

                            progress?.Report(transferProgress);
                        }
                    }

                    await outputStream.FlushAsync(cancellationToken);

                    // gzipStream/outputStream disposed here.
                }

                // compressedStream also disposed here.

                progress?.Report(
                    new FileTransferProgress(
                        BytesTransferred:
                            compressedLength,
                        TotalBytes:
                            compressedLength));

                File.Move(
                    partialPath,
                    destinationPath,
                    overwrite: true);
            }
            catch
            {
                DeleteIfExists(partialPath);
                throw;
            }
        }
        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
