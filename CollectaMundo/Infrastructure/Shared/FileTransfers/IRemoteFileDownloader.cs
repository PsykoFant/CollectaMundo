using CollectaMundo.Infrastructure.Shared.IO;

namespace CollectaMundo.Infrastructure.Shared.FileTransfers
{
    public interface IRemoteFileDownloader
    {
        Task<long> DownloadAsync(string url, string destinationPath, IProgress<FileTransferProgress>? progress = null, CancellationToken cancellationToken = default);
    }
}
