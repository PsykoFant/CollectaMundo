using CollectaMundo.ViewModels;

namespace CollectaMundo.ApplicationServices
{
    public interface IResourceDownloader
    {
        Task<bool> DownloadAsync(string url, string targetPath, string description, bool showProgress, StatusViewModel statusVm);
    }
}
