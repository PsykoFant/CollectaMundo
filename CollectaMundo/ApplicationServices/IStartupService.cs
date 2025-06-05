using CollectaMundo.ViewModels;

namespace CollectaMundo.ApplicationServices
{
    public interface IStartupService
    {
        Task AppStartEntryPoint(StatusViewModel statusVm);
    }
}
