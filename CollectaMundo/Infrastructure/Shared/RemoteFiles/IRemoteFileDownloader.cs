using CollectaMundo.Infrastructure.Shared.IO;

namespace CollectaMundo.Infrastructure.Shared.RemoteFiles
{
    public interface IRemoteFileDownloader
    {
        Task<long> DownloadAsync(string url, string destinationPath, IProgress<FileTransferProgress>? progress = null, CancellationToken cancellationToken = default);
    }
}
