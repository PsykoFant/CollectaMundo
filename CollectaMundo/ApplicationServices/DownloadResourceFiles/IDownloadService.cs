using CollectaMundo.ApplicationServices.Utilities;

namespace CollectaMundo.ApplicationServices.DownloadResourceFiles
{
    public interface IDownloadService
    {
        Task<OperationResult> DownloadAsync(string url, string targetPath, string label, IProgress<string>? detailProgress = null, IProgress<int>? percentProgress = null, CancellationToken token = default);
        Task<OperationResult> DownloadParallelAsync(
            string url1, string targetPath1, string label1,
            string url2, string targetPath2, string label2,
            IProgress<string>? detailProgress = null, IProgress<int>? percentProgress = null, IProgress<string>? stepLabelProgress = null, CancellationToken token = default);
    }
}
