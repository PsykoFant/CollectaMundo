using CollectaMundo.Infrastructure.Shared.IO;

namespace CollectaMundo.Infrastructure.Shared.RemoteFiles
{
    public interface IGzipFileDecompressor
    {
        Task DecompressAsync(string gzipPath, string destinationPath, IProgress<FileTransferProgress>? progress = null, CancellationToken cancellationToken = default);
    }
}
