using CollectaMundo.Infrastructure.Shared.IO;

namespace CollectaMundo.Infrastructure.Shared.FileTransfers
{
    public interface IGzipFileDecompressor
    {
        Task DecompressAsync(string gzipPath, string destinationPath, IProgress<FileTransferProgress>? progress = null, CancellationToken cancellationToken = default);
    }
}
