using CollectaMundo.ApplicationServices.Utilities;

namespace CollectaMundo.ApplicationServices.UpdateDB
{
    public interface IUpdateService
    {
        Task<OperationResult> CheckForDbUpdatesAsync();
        Task<OperationResult> UpdateDbAsync(IProgress<string> statusLabel2Progress, IProgress<string> statusLabel3Progress, IProgress<int> percentProgress);
    }
}
