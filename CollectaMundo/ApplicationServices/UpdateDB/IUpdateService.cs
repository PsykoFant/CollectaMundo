using CollectaMundo.ApplicationServices.Utilities;

namespace CollectaMundo.ApplicationServices.UpdateDB
{
    public interface IUpdateService
    {
        Task<OperationResult> CheckForDbUpdatesAsync();
    }
}
