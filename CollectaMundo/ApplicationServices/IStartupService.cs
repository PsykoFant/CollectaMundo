using CollectaMundo.ViewModels;

namespace CollectaMundo.ApplicationServices
{
    public interface IStartupService
    {
        Task EnsureDatabaseIntegrityAsync(StatusViewModel statusVm);
    }
}
