using CollectaMundo.ViewModels;

namespace CollectaMundo.ApplicationServices
{
    public interface ICardDatabasePreparationService
    {
        Task<bool> DownloadResourceAsync(string url, string targetPath, string description, bool showProgress, StatusViewModel statusVm);
    }
}
