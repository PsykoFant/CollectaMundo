using CollectaMundo.ApplicationServices.Utilities;

namespace CollectaMundo.ApplicationServices.UpdateDB
{
    public interface IUpdateService
    {
        Task<OperationResult> CheckForDbUpdatesAsync();
        Task<OperationResult> UpdateDbAsync(IProgress<string> statusProgress, IProgress<int> percentProgress);
    }
}
